using System.Diagnostics;
using AgriStream.Functions.Models;
using AgriStream.Shared;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

// Blob trigger
public class ProcessDroneImage
{
    private readonly ILogger<ProcessDroneImage> _logger;

    public ProcessDroneImage(ILogger<ProcessDroneImage> logger) => _logger = logger;

    [Function(nameof(ProcessDroneImage))]
    [TableOutput(StorageNames.ImageAuditTable, Connection = "StorageConnection")]
    public async Task<ImageAuditEntity> Run(
        [BlobTrigger(StorageNames.DroneImagesContainer + "/{fieldId}/{fileName}", Connection = "StorageConnection")] BlobClient blob,
        string fieldId,
        string fileName,
        FunctionContext context)
    {
        var timer = Stopwatch.StartNew();
        var properties = (await blob.GetPropertiesAsync()).Value;

        properties.Metadata.TryGetValue(DroneImageNaming.MetadataKeys.UploadedBy, out var uploadedBy);
        properties.Metadata.TryGetValue(DroneImageNaming.MetadataKeys.OriginalName, out var encodedName);
        var originalName = encodedName is null ? fileName : Uri.UnescapeDataString(encodedName);

        // Server-side validation
        var status = "Processed";
        string? reason = null;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!DroneImageNaming.AllowedExtensions.Contains(extension))
            (status, reason) = ("Rejected", $"Unsupported file type '{extension}'.");
        else if (properties.ContentType is null || !properties.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            (status, reason) = ("Rejected", $"Content type '{properties.ContentType}' is not an image.");
        else if (properties.ContentLength > DroneImageNaming.MaxFileBytes)
            (status, reason) = ("Rejected", "File is larger than 20 MB.");

        timer.Stop();

        _logger.LogInformation(
            "Drone image {BlobName}: field {FieldId}, {Bytes} bytes, {ContentType}, uploaded by {UploadedBy} -> {Status} in {Ms} ms",
            blob.Name, fieldId, properties.ContentLength, properties.ContentType, uploadedBy ?? "unknown", status, timer.Elapsed.TotalMilliseconds);

        var (partitionKey, rowKey) = DroneImageNaming.GetAuditKeys(blob.Name);

        return new ImageAuditEntity
        {
            PartitionKey = partitionKey,
            RowKey = rowKey,
            BlobName = blob.Name,
            BlobUri = blob.Uri.ToString(),
            FieldId = fieldId,
            OriginalName = originalName,
            UploadedBy = uploadedBy,
            ContentType = properties.ContentType,
            SizeBytes = properties.ContentLength,
            UploadedAtUtc = properties.CreatedOn.UtcDateTime,
            ProcessedAtUtc = DateTime.UtcNow,
            Status = status,
            StatusReason = reason,
            ProcessingMs = timer.Elapsed.TotalMilliseconds,
            InvocationId = context.InvocationId
        };
    }
}