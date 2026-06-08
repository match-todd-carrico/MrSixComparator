namespace MrSixResultsComparator.Core.Diffing;

/// <summary>
/// Tuning for <see cref="JsonDiff"/>. The defaults are deliberately "hide nothing": for a
/// server-parity diagnostic tool a timestamp / TTL difference may itself be the bug, so the
/// default <see cref="IgnoreKeys"/> set is empty. Populate it to suppress known-noisy fields.
/// </summary>
public sealed class JsonDiffOptions
{
    /// <summary>
    /// Object property names treated as always-equal (never counted as a difference), matched on
    /// leaf name only. Empty by default — nothing is suppressed.
    /// </summary>
    public IReadOnlyCollection<string> IgnoreKeys { get; init; } = Array.Empty<string>();

    public bool IgnoreKeysCaseInsensitive { get; init; } = true;

    /// <summary>
    /// Candidate id-like property names, in priority order, used to align arrays of objects so a
    /// reordered list does not read as fully changed. The first candidate present and unique on
    /// every element of BOTH sides wins.
    /// </summary>
    public IReadOnlyList<string> ArrayIdKeys { get; init; } = new[]
    {
        "id", "Id", "ID", "userId", "UserId", "otherUserId", "userid", "key", "name", "type",
    };

    /// <summary>Max array elements compared/rendered per array before truncating (guards pathological payloads).</summary>
    public int MaxArrayChildren { get; init; } = 500;

    /// <summary>Recursion depth guard against pathologically nested payloads.</summary>
    public int MaxDepth { get; init; } = 64;

    public static JsonDiffOptions Default { get; } = new();
}
