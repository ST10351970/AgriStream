using AgriStream.Web.Data;
using AgriStream.Web.Models.ViewModels;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

// Field dashboard. Calls the HTTP GET Function for latest telemetry
[Authorize]
public class DashboardController : Controller
{
    private readonly AgriStreamDbContext _db;
    private readonly FunctionsApiClient _functions;

    public DashboardController(AgriStreamDbContext db, FunctionsApiClient functions)
    {
        _db = db;
        _functions = functions;
    }

    public async Task<IActionResult> Index(string? fieldId, CancellationToken ct)
    {
        var fields = await _db.Devices.AsNoTracking()
            .Select(d => d.FieldId).Distinct().OrderBy(f => f).ToListAsync(ct);

        var selected = !string.IsNullOrWhiteSpace(fieldId) && fields.Contains(fieldId)
            ? fieldId
            : fields.FirstOrDefault();

        var model = new DashboardViewModel { Fields = fields, SelectedField = selected };
        if (selected is null)
            return View(model);

        var result = await _functions.GetLatestTelemetryAsync(selected, 50, ct);
        model.FetchMilliseconds = result.ElapsedMs;

        if (!result.Success || result.Data is null)
        {
            model.Error = result.Error;
            return View(model);
        }

        var readings = result.Data;
        model.Readings = readings.OrderByDescending(r => r.TimestampUtc).ToList();
        model.LatestBySensor = readings
            .GroupBy(r => r.SensorId)
            .Select(g => g.OrderByDescending(r => r.TimestampUtc).First())
            .OrderBy(r => r.SensorId)
            .ToList();

        var ordered = readings.OrderBy(r => r.TimestampUtc).ToList();
        var times = ordered.Select(r => r.TimestampUtc).Distinct().ToList();
        model.ChartLabels = times.Select(t => t.ToString("HH:mm:ss")).ToList();
        model.ChartSeries = ordered
            .GroupBy(r => r.SensorId)
            .Select(g => new ChartSeries(
                g.Key,
                times.Select(t => g.FirstOrDefault(r => r.TimestampUtc == t)?.SoilMoisturePercent).ToList()))
            .ToList();

        return View(model);
    }
}