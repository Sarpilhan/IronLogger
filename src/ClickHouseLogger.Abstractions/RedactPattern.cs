using System.Text.RegularExpressions;

namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Describes a named regex pattern used for PII redaction.
/// </summary>
/// <param name="Name">Human-readable name for diagnostics (e.g., "Email", "CreditCard").</param>
/// <param name="Pattern">Compiled regex to match sensitive data.</param>
/// <param name="Replacement">The mask string that replaces matches.</param>
public sealed record RedactPattern(string Name, Regex Pattern, string Replacement);
