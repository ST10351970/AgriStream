using System.Diagnostics;
using AgriStream.Shared;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

// HTTP GET TRIGGER
// TABLE INPUT BINDING. Runtime injects a TableClient for SensorTelemetry
public class GetTelemetry
{
    private const int DefaultTop = 50;
    private const int MaxTop = 200;
    private readonly ILogger<GetTelemetry> _logger;

    public GetTelemetry(ILogger<GetTelemetry> logger) => _logger = logger;

    [Function(nameof(GetTelemetry))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/{fieldId}")] HttpRequest req,
        string fieldId,
        [TableInput(StorageNames.TelemetryTable, Connection = "StorageConnection")] TableClient table)
    {
        if (string.IsNullOrWhiteSpace(fieldId) || fieldId.Length > 50)
            return new BadRequestObjectResult(new { error = "A valid fieldId is required." });

        var top = int.TryParse(req.Query["top"], out var requested)
            ? Math.Clamp(requested, 1, MaxTop)
            : DefaultTop;

        var timer = Stopwatch.StartNew();
        var readings = new List<TelemetryReading>(top);

        await foreach (var entity in table.QueryAsync<TelemetryEntity>(e => e.PartitionKey == fieldId, maxPerPage: top))
        {
            readings.Add(entity.ToReading());
            if (readings.Count >= top) break;
        }

        timer.Stop();
        _logger.LogInformation("Returned {Count} telemetry readings for {FieldId} in {Ms} ms", readings.Count, fieldId, timer.ElapsedMilliseconds);

        req.HttpContext.Response.Headers.CacheControl = "no-store"; 
        return new OkObjectResult(readings);
    }
}