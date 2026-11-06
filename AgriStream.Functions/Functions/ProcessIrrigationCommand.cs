using System.Data;
using AgriStream.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// QUEUE TRIGGER: processes irrigation commands from the irrigation-commands queue and updates
/// the device master registry in Azure SQL
/// </summary>
public class ProcessIrrigationCommand
{
    private const int PrimaryKeyViolation = 2627;
    private readonly ILogger<ProcessIrrigationCommand> _logger;
    private readonly string _sqlConnectionString;

    public ProcessIrrigationCommand(ILogger<ProcessIrrigationCommand> logger, IConfiguration configuration)
    {
        _logger = logger;
        _sqlConnectionString = configuration["SqlConnectionString"]
            ?? throw new InvalidOperationException("App setting 'SqlConnectionString' is not configured.");
    }

    [Function(nameof(ProcessIrrigationCommand))]
    public async Task Run(
        [QueueTrigger(StorageNames.IrrigationQueue, Connection = "StorageConnection")] IrrigationCommandRequest command,
        FunctionContext context)
    {
        context.BindingContext.BindingData.TryGetValue("DequeueCount", out var dequeueCount);
        _logger.LogInformation("Processing irrigation command {CommandId} for {DeviceId} (attempt {Attempt})",
            command.CommandId, command.DeviceId, dequeueCount);

        try
        {
            await using var connection = new SqlConnection(_sqlConnectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            // 1. Idempotency check
            await using (var check = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.IrrigationCommands WHERE CommandId = @CommandId", connection, transaction))
            {
                check.Parameters.Add("@CommandId", SqlDbType.UniqueIdentifier).Value = command.CommandId;
                if ((int)(await check.ExecuteScalarAsync() ?? 0) > 0)
                {
                    _logger.LogWarning("Duplicate command {CommandId} ignored (already processed).", command.CommandId);
                    await transaction.RollbackAsync();
                    return;
                }
            }

            // 2. Validate device against the master registry
            string? deviceType = null, currentStatus = null;
            await using (var lookup = new SqlCommand(
                "SELECT DeviceType, OperationalStatus FROM dbo.Devices WITH (UPDLOCK, ROWLOCK) WHERE DeviceId = @DeviceId",
                connection, transaction))
            {
                lookup.Parameters.Add("@DeviceId", SqlDbType.NVarChar, 50).Value = command.DeviceId;
                await using var reader = await lookup.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    deviceType = reader.GetString(0);
                    currentStatus = reader.GetString(1);
                }
            }

            if (deviceType != "IrrigationValve" || currentStatus is "Fault" or "Offline")
            {
                _logger.LogWarning("Command {CommandId} rejected: device {DeviceId} is {Type}/{Status}.",
                    command.CommandId, command.DeviceId, deviceType ?? "not found", currentStatus ?? "-");
                await transaction.RollbackAsync();
                return; 
            }

            // 3. Update device status and record the command
            await using (var update = new SqlCommand(@"
                UPDATE dbo.Devices
                   SET OperationalStatus = 'Irrigating',
                       LastCommandId     = @CommandId,
                       LastUpdatedUtc    = SYSUTCDATETIME()
                 WHERE DeviceId = @DeviceId;

                INSERT INTO dbo.IrrigationCommands
                       (CommandId, DeviceId, DurationMinutes, Source, RequestedBy, RequestedAtUtc)
                VALUES (@CommandId, @DeviceId, @DurationMinutes, @Source, @RequestedBy, @RequestedAtUtc);",
                connection, transaction))
            {
                update.Parameters.Add("@CommandId", SqlDbType.UniqueIdentifier).Value = command.CommandId;
                update.Parameters.Add("@DeviceId", SqlDbType.NVarChar, 50).Value = command.DeviceId;
                update.Parameters.Add("@DurationMinutes", SqlDbType.Int).Value = command.DurationMinutes;
                update.Parameters.Add("@Source", SqlDbType.NVarChar, 30).Value = command.Source;
                update.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = (object?)command.RequestedBy ?? DBNull.Value;
                update.Parameters.Add("@RequestedAtUtc", SqlDbType.DateTime2).Value = command.RequestedAtUtc;
                await update.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            var latency = DateTime.UtcNow - DateTime.SpecifyKind(command.RequestedAtUtc, DateTimeKind.Utc);
            _logger.LogInformation("Device {DeviceId} set to Irrigating for {Minutes} min. End-to-end latency {Ms} ms.",
                command.DeviceId, command.DurationMinutes, latency.TotalMilliseconds);
        }
        catch (SqlException ex) when (ex.Number == PrimaryKeyViolation)
        {
            _logger.LogWarning("Command {CommandId} was processed concurrently by another instance - ignored.", command.CommandId);
        }
    }
}