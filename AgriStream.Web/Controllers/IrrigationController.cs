using AgriStream.Shared;
using AgriStream.Web.Data;
using AgriStream.Web.Models.ViewModels;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

// Trigger irrigation. calls the HTTP POST Function and places command on Storage Queue
[Authorize(Roles = AppRoles.Farmer + "," + AppRoles.Admin)]
public class IrrigationController : Controller
{
    private readonly AgriStreamDbContext _db;
    private readonly FunctionsApiClient _functions;

    public IrrigationController(AgriStreamDbContext db, FunctionsApiClient functions)
    {
        _db = db;
        _functions = functions;
    }

    public async Task<IActionResult> Index(string? fieldId, CancellationToken ct)
    {
        var valves = _db.Devices.AsNoTracking().Where(d => d.DeviceType == "IrrigationValve");

        var model = new IrrigationViewModel
        {
            Fields = await valves.Select(d => d.FieldId).Distinct().OrderBy(f => f).ToListAsync(ct),
            SelectedField = fieldId,
            Valves = await (string.IsNullOrWhiteSpace(fieldId) ? valves : valves.Where(d => d.FieldId == fieldId))
                .OrderBy(d => d.FieldId).ThenBy(d => d.DeviceId)
                .ToListAsync(ct)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trigger(string deviceId, int durationMinutes, string? fieldId, CancellationToken ct)
    {
        if (durationMinutes is < 1 or > 240)
        {
            TempData["Error"] = "Duration must be between 1 and 240 minutes.";
            return RedirectToAction(nameof(Index), new { fieldId });
        }

        var valve = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.DeviceType == "IrrigationValve", ct);

        if (valve is null)
        {
            TempData["Error"] = "Unknown irrigation valve.";
            return RedirectToAction(nameof(Index), new { fieldId });
        }

        var command = new IrrigationCommandRequest(
            CommandId: Guid.NewGuid(),
            DeviceId: valve.DeviceId,
            FieldId: valve.FieldId,
            DurationMinutes: durationMinutes,
            Source: IrrigationSources.User,
            RequestedBy: User.Identity?.Name,
            RequestedAtUtc: DateTime.UtcNow);

        var result = await _functions.QueueIrrigationAsync(command, ct);

        if (result.Success)
            TempData["Success"] = $"Irrigation command {command.CommandId.ToString()[..8]} queued for {valve.DeviceId} " +
                                  $"({durationMinutes} min) in {result.ElapsedMs} ms. The device status updates once the queue is processed.";
        else
            TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Index), new { fieldId });
    }
}