namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Serializes a batch of <see cref="LogEvent"/> instances to a byte representation
/// suitable for ClickHouse ingestion (NDJSON / JSONEachRow format).
/// <para>
/// Implementations should use <see cref="System.Text.Json.Utf8JsonWriter"/> with
/// pooled buffers (<see cref="System.Buffers.ArrayPool{T}"/>) to minimize allocations.
/// </para>
/// </summary>
public interface ILogEventSerializer
{
    /// <summary>
    /// Serializes a batch of log events to NDJSON-encoded UTF-8 bytes.
    /// Each event is a single JSON object followed by a newline (<c>\n</c>).
    /// </summary>
    /// <param name="batch">The log events to serialize.</param>
    /// <returns>NDJSON payload as a byte array. Ownership is transferred to the caller.</returns>
    byte[] Serialize(IReadOnlyList<LogEvent> batch);
}
