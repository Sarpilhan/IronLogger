namespace ClickHouseLogger.Abstractions;

/// <summary>
/// HTTP body compression mode for ClickHouse ingestion.
/// </summary>
public enum ClickHouseCompression
{
    /// <summary>No compression. Lower CPU, higher bandwidth.</summary>
    None = 0,

    /// <summary>
    /// Gzip compression. Recommended default — reduces payload size by ~80-90%.
    /// Adds <c>Content-Encoding: gzip</c> header.
    /// </summary>
    Gzip = 1
}
