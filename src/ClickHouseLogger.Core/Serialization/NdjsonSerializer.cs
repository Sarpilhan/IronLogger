using System.Buffers;
using System.Globalization;
using System.Text.Json;
using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Serialization;

/// <summary>
/// Serializes <see cref="LogEvent"/> batches to NDJSON (JSONEachRow) format
/// using <see cref="Utf8JsonWriter"/> for minimal allocations.
/// <para>
/// Output format: one JSON object per line, newline-delimited.
/// Column names match the ClickHouse <c>app_logs</c> schema exactly.
/// </para>
/// <para>
/// Implementation notes:
/// <list type="bullet">
///   <item>Uses <see cref="ArrayBufferWriter{T}"/> to accumulate output bytes</item>
///   <item>Reuses <see cref="Utf8JsonWriter"/> across events via <c>Reset()</c></item>
///   <item>Optional/null fields are omitted to reduce payload size</item>
///   <item>Props are serialized as a JSON object (ClickHouse <c>Map(String, String)</c>)</item>
/// </list>
/// </para>
/// </summary>
internal sealed class NdjsonSerializer : ILogEventSerializer
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    private static readonly string[] LevelNames =
    [
        "Trace",        // 0
        "Debug",        // 1
        "Information",  // 2
        "Warning",      // 3
        "Error",        // 4
        "Critical",     // 5
        "None"          // 6
    ];

    private readonly JsonWriterOptions _writerOptions = new()
    {
        // Skip validation for performance — we control the output structure
        SkipValidation = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly int _maxExceptionLength;

    /// <summary>
    /// Initializes a new <see cref="NdjsonSerializer"/>.
    /// </summary>
    /// <param name="maxExceptionLength">Maximum exception string length in characters.</param>
    public NdjsonSerializer(int maxExceptionLength = Defaults.MaxExceptionLength)
    {
        _maxExceptionLength = maxExceptionLength;
    }

    /// <inheritdoc />
    public byte[] Serialize(IReadOnlyList<LogEvent> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Count == 0)
            return [];

        // Estimate ~400 bytes per event for initial buffer sizing
        var buffer = new ArrayBufferWriter<byte>(batch.Count * 400);
        var writer = new Utf8JsonWriter(buffer, _writerOptions);

        try
        {
            for (var i = 0; i < batch.Count; i++)
            {
                WriteEvent(writer, batch[i]);
                writer.Flush();

                // Write newline separator between (and after) events
                buffer.Write(NewLine);

                // Reset writer for next event — buffer position is preserved
                writer.Reset(buffer);
            }
        }
        finally
        {
            writer.Dispose();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private void WriteEvent(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();

        // ── Required fields ────────────────────────────────────
        WriteTimestamp(writer, evt.Timestamp);
        writer.WriteString("level"u8, GetLevelName(evt.Level));
        writer.WriteString("message"u8, evt.Message);
        writer.WriteString("category"u8, evt.Category);
        writer.WriteString("service"u8, evt.Service);
        writer.WriteString("env"u8, evt.Environment);

        // ── Optional fields (omit if null/empty) ───────────────
        WriteOptionalString(writer, "template"u8, evt.Template);
        WriteException(writer, evt.Exception);
        WriteOptionalString(writer, "trace_id"u8, evt.TraceId);
        WriteOptionalString(writer, "span_id"u8, evt.SpanId);
        WriteOptionalString(writer, "correlation_id"u8, evt.CorrelationId);
        WriteOptionalString(writer, "version"u8, evt.Version);
        WriteOptionalString(writer, "host"u8, evt.Host);

        // ── Props (always present, may be empty object) ────────
        WriteProps(writer, evt.Props);

        writer.WriteEndObject();
    }

    private static void WriteTimestamp(Utf8JsonWriter writer, DateTimeOffset timestamp)
    {
        // Format: "2024-01-15 10:30:45.123" — ClickHouse DateTime64(3) compatible
        Span<char> formatted = stackalloc char[23];
        timestamp.UtcDateTime.TryFormat(formatted, out _, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        writer.WriteString("ts"u8, formatted);
    }

    private static string GetLevelName(LogEventLevel level)
    {
        var index = (int)level;
        return (uint)index < (uint)LevelNames.Length ? LevelNames[index] : "Unknown";
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, ReadOnlySpan<byte> propertyName, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            writer.WriteString(propertyName, value);
        }
    }

    private void WriteException(Utf8JsonWriter writer, string? exception)
    {
        if (string.IsNullOrEmpty(exception))
            return;

        var value = exception.Length > _maxExceptionLength
            ? exception[.._maxExceptionLength]
            : exception;

        writer.WriteString("exception"u8, value);
    }

    private static void WriteProps(Utf8JsonWriter writer, Dictionary<string, string> props)
    {
        writer.WriteStartObject("props"u8);

        if (props.Count > 0)
        {
            foreach (var kvp in props)
            {
                writer.WriteString(kvp.Key, kvp.Value);
            }
        }

        writer.WriteEndObject();
    }
}
