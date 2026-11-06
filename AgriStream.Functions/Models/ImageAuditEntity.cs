using Azure;
using Azure.Data.Tables;

namespace AgriStream.Functions.Models;

// Row written to ImageAudit table by Blob-triggered function
public class ImageAuditEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;   // FieldId
    public string RowKey { get; set; } = string.Empty;         
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string BlobName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public string FieldId { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public string? UploadedBy { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;         // Processed | Rejected
    public string? StatusReason { get; set; }
    public double ProcessingMs { get; set; }
    public string? InvocationId { get; set; }
}