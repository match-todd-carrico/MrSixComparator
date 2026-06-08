namespace MrSixResultsComparator.Core.Diffing;

public enum DiffMode
{
    /// <summary>Both sides parsed as JSON; <see cref="EndpointDiffResult.Tree"/> holds the structural diff.</summary>
    Structural,
    /// <summary>Exactly one side is JSON; the other is an error / empty / non-JSON sentinel.</summary>
    SingleSide,
    /// <summary>Neither side is JSON.</summary>
    BothNonJson,
}

/// <summary>
/// The result of diffing a single endpoint's Control vs Test response. Covers the happy path
/// (<see cref="DiffMode.Structural"/>) plus the two degraded cases as first-class states so the UI
/// never has to re-parse or guess.
/// </summary>
public sealed class EndpointDiffResult
{
    public required DiffMode Mode { get; init; }

    /// <summary>The structural diff tree (only set when <see cref="Mode"/> is <see cref="DiffMode.Structural"/>).</summary>
    public JsonDiffNode? Tree { get; init; }

    /// <summary>Structural change count (0 for the non-structural modes).</summary>
    public int ChangeCount { get; init; }

    /// <summary>True when the two sides are equivalent (no structural diff, or byte-equal non-JSON).</summary>
    public bool Identical { get; init; }

    /// <summary>Human-readable explanation for the degraded modes.</summary>
    public string? Notice { get; init; }

    public string? RawControl { get; init; }
    public string? RawTest { get; init; }

    public bool ControlIsJson { get; init; }
    public bool TestIsJson { get; init; }
}

/// <summary>Pairs a labelled endpoint with its diff result, for rendering a user's probe card.</summary>
public sealed record EndpointDiffEntry(string Label, EndpointDiffResult Result);

/// <summary>A single rendered line of a flattened structural diff (see <see cref="JsonDiff.Flatten"/>).</summary>
public sealed record DiffLine(string Text, DiffStatus Status, bool Foldable, bool Muted = false);
