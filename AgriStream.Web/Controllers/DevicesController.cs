using AgriStream.Web.Data;
using AgriStream.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

// Device master registry (Azure SQL). Visible to all signed-in roles
[Authorize]
public class DevicesController : Controller
{
    private readonly AgriStreamDbContext _db;

    public DevicesController(AgriStreamDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? fieldId)
    {
        var fields = await _db.Devices
            .AsNoTracking()
            .Select(d => d.FieldId)
            .Distinct()
            .OrderBy(f => f)
            .ToListAsync();

        var query = _db.Devices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(fieldId))
            query = query.Where(d => d.FieldId == fieldId);

        var model = new DevicesIndexViewModel
        {
            Fields = fields,
            SelectedField = fieldId,
            Devices = await query.OrderBy(d => d.FieldId).ThenBy(d => d.DeviceId).ToListAsync(),
            RecentCommands = await _db.IrrigationCommands
                .AsNoTracking()
                .OrderByDescending(c => c.ProcessedAtUtc)
                .Take(10)
                .ToListAsync()
        };

        return View(model);
    }
}