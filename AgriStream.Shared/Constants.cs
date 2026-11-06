namespace AgriStream.Shared;

/// <summary>
/// Single source of truth for Azure Storage resource names.
/// Used by the MVC app, the Functions and the simulator.
/// </summary>
public static class StorageNames
{
    public const string DroneImagesContainer = "drone-images";        // Blob container: drone images
    public const string TelemetryTable = "SensorTelemetry";     // Table: high-frequency sensor readings
    public const string ImageAuditTable = "ImageAudit";          // Table: blob-trigger processing audit log
    public const string IrrigationQueue = "irrigation-commands"; // Queue: irrigation commands
}

/// <summary>
/// System roles, stored in the Roles table in Azure SQL.
/// </summary>
public static class AppRoles
{
    public const string Farmer = "Farmer";
    public const string Agronomist = "Agronomist";
    public const string Admin = "Admin";
}