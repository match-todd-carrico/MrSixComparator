using Newtonsoft.Json.Linq;

namespace MrSixResultsComparator.Core.Diffing;

/// <summary>The diff classification of a single node in the merged Control-vs-Test JSON tree.</summary>
public enum DiffStatus
{
    /// <summary>Identical on both sides.</summary>
    Same,
    /// <summary>Present on Control, absent on Test (reads as a deletion).</summary>
    ControlOnly,
    /// <summary>Present on Test, absent on Control (reads as an addition).</summary>
    TestOnly,
    /// <summary>Scalar value or token kind differs between the two sides.</summary>
    Changed,
    /// <summary>An object/array whose own identity is unchanged but which contains differing descendants.</summary>
    ContainerChanged,
}

public enum JsonNodeKind
{
    Object,
    Array,
    Value,
}

/// <summary>
/// How a JArray's elements were aligned for comparison. Surfaced in the UI so a reviewer
/// knows which heuristic produced the result (e.g. an order-insensitive multiset compare).
/// </summary>
public enum ArrayMatchMode
{
    None,
    ById,
    Multiset,
    ByIndex,
}

/// <summary>
/// One node of the structural diff tree produced by <see cref="JsonDiff"/>. Immutable; a plain
/// class (not a record) so the lazily-irrelevant <see cref="ChangeCount"/> aggregation stays out
/// of value-equality and recursion is cheap to build.
/// </summary>
public sealed class JsonDiffNode
{
    public string Key { get; }
    public JsonNodeKind Kind { get; }
    public DiffStatus Status { get; }

    /// <summary>The Control-side token, where one exists (null for <see cref="DiffStatus.TestOnly"/>).</summary>
    public JToken? Control { get; }

    /// <summary>The Test-side token, where one exists (null for <see cref="DiffStatus.ControlOnly"/>).</summary>
    public JToken? Test { get; }

    public IReadOnlyList<JsonDiffNode> Children { get; }
    public ArrayMatchMode ArrayMode { get; }

    /// <summary>Synthetic node standing in for elements omitted by a render/compare cap.</summary>
    public bool IsTruncation { get; }

    /// <summary>
    /// Number of changed leaves in this subtree (leaves whose status is ControlOnly / TestOnly /
    /// Changed). Container nodes aggregate their children; structural <see cref="DiffStatus.ContainerChanged"/>
    /// nodes are not themselves counted. Drives the "N differences" chip and the collapse decision.
    /// </summary>
    public int ChangeCount { get; }

    public bool HasChange => ChangeCount > 0;

    public JsonDiffNode(
        string key,
        JsonNodeKind kind,
        DiffStatus status,
        JToken? control = null,
        JToken? test = null,
        IReadOnlyList<JsonDiffNode>? children = null,
        ArrayMatchMode arrayMode = ArrayMatchMode.None,
        bool isTruncation = false)
    {
        Key = key;
        Kind = kind;
        Status = status;
        Control = control;
        Test = test;
        Children = children ?? Array.Empty<JsonDiffNode>();
        ArrayMode = arrayMode;
        IsTruncation = isTruncation;
        ChangeCount = ComputeChangeCount(status, Children);
    }

    private static int ComputeChangeCount(DiffStatus status, IReadOnlyList<JsonDiffNode> children)
    {
        if (children.Count == 0)
            return status is DiffStatus.ControlOnly or DiffStatus.TestOnly or DiffStatus.Changed ? 1 : 0;

        int sum = 0;
        for (int i = 0; i < children.Count; i++)
            sum += children[i].ChangeCount;
        return sum;
    }
}
