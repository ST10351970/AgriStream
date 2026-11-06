using AgriStream.Shared;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Globalization;

namespace AgriStream.Web.Services;

public record DroneImageItem(
    string BlobName,
    string FieldId,
    string OriginalName,
    string? UploadedBy,
    long SizeBytes,
    DateTimeOffset? UploadedAt,
    string? PreviewUrl,
    string AuditStatus,
    DateTimeOffset? ProcessedAt);

// Uploads drone images to Azure Blob Storage
public class DroneImageService
{
    private readonly BlobContainerClient _container;
    private readonly TableClient _auditTable;
    private readonly ILogger<DroneImageService> _logger;

    public DroneImageService(BlobServiceClient blobService, TableServiceClient tableService, ILogger<DroneImageService> logger)
    {
        _container = blobService.GetBlobContainerClient(StorageNames.DroneImagesContainer);
        _auditTable = tableService.GetTableClient(StorageNames.ImageAuditTable);
        _logger = logger;
    }

    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return "Choose an image to upload.";

        if (file.Length > DroneImageNaming.MaxFileBytes)
            return $"Images must be {DroneImageNaming.MaxFileBytes / 1024 / 1024} MB or smaller.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!DroneImageNaming.AllowedExtensions.Contains(extension))
            return "Only JPG, PNG, TIFF or WEBP images are allowed.";

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "The selected file is not an image.";

        return null;
    }

    public async Task<string> UploadAsync(IFormFile file, string fieldId, string uploadedBy, CancellationToken ct)
    {
        var blobName = DroneImageNaming.BuildBlobName(fieldId, file.FileName);
        var blobClient = _container.GetBlobClient(blobName);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType },
            Metadata = new Dictionary<string, string>
            {
                [DroneImageNaming.MetadataKeys.FieldId] = fieldId,
                [DroneImageNaming.MetadataKeys.UploadedBy] = uploadedBy,
                [DroneImageNaming.MetadataKeys.OriginalName] = Uri.EscapeDataString(Path.GetFileName(file.FileName))
            }
        };

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, options, ct);

        _logger.LogInformation("Drone image {BlobName} ({Bytes} bytes) uploaded by {User}", blobName, file.Length, uploadedBy);
        return blobName;
    }

    public async Task<IReadOnlyList<DroneImageItem>> GetRecentAsync(int take, CancellationToken ct)
    {
        var blobs = new List<BlobItem>();
        await foreach (var blob in _container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, null, ct))
            blobs.Add(blob);

        var results = new List<DroneImageItem>();

        foreach (var blob in blobs.OrderByDescending(b => b.Properties.LastModified).Take(take))
        {
            blob.Metadata.TryGetValue(DroneImageNaming.MetadataKeys.FieldId, out var fieldId);
            blob.Metadata.TryGetValue(DroneImageNaming.MetadataKeys.UploadedBy, out var uploadedBy);
            blob.Metadata.TryGetValue(DroneImageNaming.MetadataKeys.OriginalName, out var originalName);

            // Read the audit entry written by Blob-triggered Function
            var status = "Pending";
            DateTimeOffset? processedAt = null;
            try
            {
                var (pk, rk) = DroneImageNaming.GetAuditKeys(blob.Name);
                var audit = await _auditTable.GetEntityIfExistsAsync<TableEntity>(pk, rk, cancellationToken: ct);
                if (audit.HasValue && audit.Value is not null)
                {
                    status = audit.Value.GetString("Status") ?? "Logged";
                    processedAt = ReadDateTime(audit.Value, "ProcessedAtUtc");
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogWarning(ex, "Could not read audit entry for {BlobName}", blob.Name);
                status = "Unknown";
            }

            var blobClient = _container.GetBlobClient(blob.Name);
            string? previewUrl = blobClient.CanGenerateSasUri
                ? blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(15)).ToString()
                : null;

            results.Add(new DroneImageItem(
                BlobName: blob.Name,
                FieldId: fieldId ?? DroneImageNaming.GetAuditKeys(blob.Name).PartitionKey,
                OriginalName: originalName is null ? blob.Name : Uri.UnescapeDataString(originalName),
                UploadedBy: uploadedBy,
                SizeBytes: blob.Properties.ContentLength ?? 0,
                UploadedAt: blob.Properties.LastModified,
                PreviewUrl: previewUrl,
                AuditStatus: status,
                ProcessedAt: processedAt));
        }

        return results;
    }

    private static DateTimeOffset? ReadDateTime(TableEntity entity, string key)
    {
        if (!entity.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
            _ => null
        };
    }
}