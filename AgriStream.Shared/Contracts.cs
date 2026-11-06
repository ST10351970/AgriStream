namespace AgriStream.Shared;

// One soil sensor reading. Returned by HTTP GET Function as JSON
public record TelemetryReading(
    string FieldId,
    string SensorId,
    DateTime TimestampUtc,
    double SoilMoisturePercent,
    double SoilTemperatureC,
    double AirTemperatureC,
    double HumidityPercent,
    double? SoilPh);

// Irrigation command sent to HTTP POST Function and placed on the Storage Queue
public record IrrigationCommandRequest(
    Guid CommandId,
    string DeviceId,
    string FieldId,
    int DurationMinutes,
    string Source,
    string? RequestedBy,
    DateTime RequestedAtUtc);

public static class IrrigationSources
{
    public const string User = "User";
    public const string Sensor = "Sensor";
}

// Naming rules shared by the web app (uploader) and the Blob-triggered Function (auditor)
public static class DroneImageNaming
{
    public const long MaxFileBytes = 20 * 1024 * 1024; // 20 MB
    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp" };

    public static class MetadataKeys
    {
        public const string FieldId = "fieldid";
        public const string UploadedBy = "uploadedby";
        public const string OriginalName = "originalname";
    }

    public static string BuildBlobName(string fieldId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var shortId = Guid.NewGuid().ToString("N")[..8];
        return $"{fieldId}/{DateTime.UtcNow:yyyyMMdd-HHmmss}-{shortId}{extension}";
    }

    public static (string PartitionKey, string RowKey) GetAuditKeys(string blobName)
    {
        var slash = blobName.IndexOf('/');
        return slash > 0
            ? (blobName[..slash], blobName[(slash + 1)..])
            : ("unassigned", blobName);
    }
}