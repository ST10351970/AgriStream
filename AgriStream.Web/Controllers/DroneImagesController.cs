using AgriStream.Web.Data;
using AgriStream.Web.Models.ViewModels;
using AgriStream.Web.Services;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize]
public class DroneImagesController : Controller
{
    private readonly DroneImageService _images;
    private readonly AgriStreamDbContext _db;
    private readonly ILogger<DroneImagesController> _logger;

    public DroneImagesController(DroneImageService images, AgriStreamDbContext db, ILogger<DroneImagesController> logger)
    {
        _images = images;
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await BuildModelAsync(new DroneUploadViewModel(), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(DroneUploadViewModel model, CancellationToken ct)
    {
        var fileError = DroneImageService.Validate(model.File);
        if (fileError is not null)
            ModelState.AddModelError(nameof(model.File), fileError);

        if (!await _db.Devices.AnyAsync(d => d.FieldId == model.FieldId, ct))
            ModelState.AddModelError(nameof(model.FieldId), "Select a valid field.");

        if (!ModelState.IsValid)
            return View("Index", await BuildModelAsync(model, ct));

        try
        {
            var blobName = await _images.UploadAsync(model.File!, model.FieldId, User.Identity!.Name!, ct);
            TempData["Success"] = $"Uploaded '{model.File!.FileName}' as {blobName}. The Blob-triggered Function will log it shortly.";
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Blob upload failed");
            TempData["Error"] = "Upload failed - the storage service is unavailable. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<DroneUploadViewModel> BuildModelAsync(DroneUploadViewModel model, CancellationToken ct)
    {
        model.Fields = await _db.Devices.AsNoTracking()
            .Select(d => d.FieldId).Distinct().OrderBy(f => f).ToListAsync(ct);

        try
        {
            model.RecentImages = await _images.GetRecentAsync(20, ct);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Could not list drone images");
            model.LoadError = "Could not load recent uploads from Blob Storage.";
        }

        return model;
    }
}