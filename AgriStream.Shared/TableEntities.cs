using Azure;
using Azure.Data.Tables;

namespace AgriStream.Shared;


// One sensor reading in SensorTelemetry table.
public class TelemetryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string SensorId { get; set; } = string.Empty;
    public DateTime ReadingTimeUtc { get; set; }
    public double SoilMoisturePercent { get; set; }
    public double SoilTemperatureC { get; set; }
    public double AirTemperatureC { get; set; }
    public double HumidityPercent { get; set; }
    public double? SoilPh { get; set; }

    public static string BuildRowKey(DateTime readingTimeUtc, string sensorId) =>
        $"{DateTime.MaxValue.Ticks - readingTimeUtc.Ticks:D19}_{sensorId}";

    public static TelemetryEntity FromReading(TelemetryReading r)
    {
        var timeUtc = DateTime.SpecifyKind(r.TimestampUtc, DateTimeKind.Utc);
        return new TelemetryEntity
        {
            PartitionKey = r.FieldId,
            RowKey = BuildRowKey(timeUtc, r.SensorId),
            SensorId = r.SensorId,
            ReadingTimeUtc = timeUtc,
            SoilMoisturePercent = r.SoilMoisturePercent,
            SoilTemperatureC = r.SoilTemperatureC,
            AirTemperatureC = r.AirTemperatureC,
            HumidityPercent = r.HumidityPercent,
            SoilPh = r.SoilPh
        };
    }

    public TelemetryReading ToReading() => new(
        FieldId: PartitionKey,
        SensorId: SensorId,
        TimestampUtc: DateTime.SpecifyKind(ReadingTimeUtc, DateTimeKind.Utc),
        SoilMoisturePercent: SoilMoisturePercent,
        SoilTemperatureC: SoilTemperatureC,
        AirTemperatureC: AirTemperatureC,
        HumidityPercent: HumidityPercent,
        SoilPh: SoilPh);
}