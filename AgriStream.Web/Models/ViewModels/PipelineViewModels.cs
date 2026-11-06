using System.ComponentModel.DataAnnotations;
using AgriStream.Shared;
using AgriStream.Web.Models.Entities;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AgriStream.Web.Models.ViewModels;

public class DroneUploadViewModel
{
    [Required, Display(Name = "Field")]
    public string FieldId { get; set; } = string.Empty;

    [Display(Name = "Drone image")]
    public IFormFile? File { get; set; }

    [ValidateNever] public IReadOnlyList<string> Fields { get; set; } = new List<string>();
    [ValidateNever] public IReadOnlyList<DroneImageItem> RecentImages { get; set; } = new List<DroneImageItem>();
    [ValidateNever] public string? LoadError { get; set; }
}

public record ChartSeries(string SensorId, IReadOnlyList<double?> Values);

public class DashboardViewModel
{
    public const double DryThresholdPercent = 25;

    public IReadOnlyList<string> Fields { get; set; } = new List<string>();
    public string? SelectedField { get; set; }
    public IReadOnlyList<TelemetryReading> Readings { get; set; } = new List<TelemetryReading>();
    public IReadOnlyList<TelemetryReading> LatestBySensor { get; set; } = new List<TelemetryReading>();
    public IReadOnlyList<string> ChartLabels { get; set; } = new List<string>();
    public IReadOnlyList<ChartSeries> ChartSeries { get; set; } = new List<ChartSeries>();
    public long FetchMilliseconds { get; set; }
    public string? Error { get; set; }

    public bool FieldIsDry => LatestBySensor.Any(r => r.SoilMoisturePercent < DryThresholdPercent);
}

public class IrrigationViewModel
{
    public IReadOnlyList<string> Fields { get; set; } = new List<string>();
    public string? SelectedField { get; set; }
    public IReadOnlyList<Device> Valves { get; set; } = new List<Device>();
}