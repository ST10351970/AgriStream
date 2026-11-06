using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AgriStream.Shared;

namespace AgriStream.Web.Services;

public class FunctionsOptions
{
    public const string SectionName = "Functions";
    public string BaseUrl { get; set; } = string.Empty;
    public string? Key { get; set; }
}

public record ApiResult<T>(bool Success, T? Data, string? Error, long ElapsedMs = 0);

// HttpClient for the HTTP-triggered Azure Functions.
public class FunctionsApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<FunctionsApiClient> _logger;

    public FunctionsApiClient(HttpClient http, ILogger<FunctionsApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    //HTTP GET Function
    public async Task<ApiResult<IReadOnlyList<TelemetryReading>>> GetLatestTelemetryAsync(string fieldId, int top, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using var response = await _http.GetAsync($"api/telemetry/{Uri.EscapeDataString(fieldId)}?top={top}", ct);
            timer.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telemetry function returned {Status} for {FieldId}", (int)response.StatusCode, fieldId);
                return new(false, null, $"The telemetry service returned HTTP {(int)response.StatusCode}.", timer.ElapsedMilliseconds);
            }

            var data = await response.Content.ReadFromJsonAsync<List<TelemetryReading>>(cancellationToken: ct)
                       ?? new List<TelemetryReading>();
            return new(true, data, null, timer.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogError(ex, "Telemetry function call failed for {FieldId}", fieldId);
            return new(false, null, "The telemetry service is currently unavailable. Please try again shortly.", timer.ElapsedMilliseconds);
        }
    }

    /// HTTP POST Function
    public async Task<ApiResult<Guid>> QueueIrrigationAsync(IrrigationCommandRequest request, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using var response = await _http.PostAsJsonAsync("api/irrigation", request, ct);
            timer.Stop();

            if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK)
                return new(true, request.CommandId, null, timer.ElapsedMilliseconds);

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Irrigation function returned {Status}: {Body}", (int)response.StatusCode, body);
            return new(false, Guid.Empty, $"The irrigation service rejected the command (HTTP {(int)response.StatusCode}).", timer.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogError(ex, "Irrigation function call failed for device {DeviceId}", request.DeviceId);
            return new(false, Guid.Empty, "The irrigation service is currently unavailable. Please try again shortly.", timer.ElapsedMilliseconds);
        }
    }
}