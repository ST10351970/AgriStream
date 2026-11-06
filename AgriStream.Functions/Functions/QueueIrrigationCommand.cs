using System.Text.Json;
using AgriStream.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

public class IrrigationQueueOutput
{
    [QueueOutput(StorageNames.IrrigationQueue, Connection = "StorageConnection")]
    public string? QueueMessage { get; set; }   // null = nothing is queued

    [HttpResult]
    public IActionResult HttpResponse { get; set; } = null!;
}

// HTTP POST TRIGGER: POST /api/irrigation
public class QueueIrrigationCommand
{
    private readonly ILogger<QueueIrrigationCommand> _logger;

    public QueueIrrigationCommand(ILogger<QueueIrrigationCommand> logger) => _logger = logger;

    [Function(nameof(QueueIrrigationCommand))]
    public async Task<IrrigationQueueOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation")] HttpRequest req)
    {
        IrrigationCommandRequest? command;
        try
        {
            command = await req.ReadFromJsonAsync<IrrigationCommandRequest>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            command = null;
        }

        var error = Validate(command);
        if (error is not null)
        {
            _logger.LogWarning("Rejected irrigation request: {Error}", error);
            return new IrrigationQueueOutput { HttpResponse = new BadRequestObjectResult(new { error }) };
        }

        _logger.LogInformation("Queued irrigation command {CommandId} for {DeviceId} ({Minutes} min, source {Source})",
            command!.CommandId, command.DeviceId, command.DurationMinutes, command.Source);

        return new IrrigationQueueOutput
        {
            QueueMessage = JsonSerializer.Serialize(command),
            HttpResponse = new AcceptedResult((string?)null, new
            {
                commandId = command.CommandId,
                status = "Queued",
                queue = StorageNames.IrrigationQueue
            })
        };
    }

    private static string? Validate(IrrigationCommandRequest? c)
    {
        if (c is null) return "Request body must be a JSON irrigation command.";
        if (c.CommandId == Guid.Empty) return "CommandId is required.";
        if (string.IsNullOrWhiteSpace(c.DeviceId) || c.DeviceId.Length > 50) return "A valid DeviceId is required.";
        if (string.IsNullOrWhiteSpace(c.FieldId) || c.FieldId.Length > 50) return "A valid FieldId is required.";
        if (c.DurationMinutes is < 1 or > 240) return "DurationMinutes must be between 1 and 240.";
        if (c.Source is not (IrrigationSources.User or IrrigationSources.Sensor)) return "Source must be 'User' or 'Sensor'.";
        return null;
    }
}