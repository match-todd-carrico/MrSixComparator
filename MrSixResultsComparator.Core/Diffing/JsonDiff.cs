using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MrSixResultsComparator.Core.Diffing;

/// <summary>
/// Schema-agnostic structural diff of two JSON documents (Control vs Test), built for comparing
/// MrSix admin-panel responses where the schema is unknown and may change between releases.
///
/// Properties: object keys compare order-insensitively; scalars compare by value with
/// <see cref="JToken.DeepEquals(JToken, JToken)"/> (so <c>1</c> == <c>1.0</c> but <c>123</c> != <c>"123"</c>);
/// arrays use one of three deterministic strategies (id-aligned objects, scalar multiset, or
/// index-wise fallback). The engine is pure (strings in, <see cref="EndpointDiffResult"/> out) and
/// never throws — every input yields a renderable result.
/// </summary>
public static class JsonDiff
{
    private static readonly JsonLoadSettings LoadSettings = new()
    {
        CommentHandling = CommentHandling.Ignore,
        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace,
        LineInfoHandling = LineInfoHandling.Ignore,
    };

    private sealed class Ctx
    {
        public required JsonDiffOptions Options { get; init; }
        public required HashSet<string> Ignore { get; init; }
    }

    // ─── Public entry points ──────────────────────────────────────────────

    public static EndpointDiffResult Diff(string? controlRaw, string? testRaw, JsonDiffOptions? options = null)
    {
        options ??= JsonDiffOptions.Default;

        bool cOk = TryParse(controlRaw, out var cTok);
        bool tOk = TryParse(testRaw, out var tTok);

        if (cOk && tOk)
        {
            var ctx = new Ctx
            {
                Options = options,
                Ignore = new HashSet<string>(
                    options.IgnoreKeys,
                    options.IgnoreKeysCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
            };
            var tree = DiffToken("$", cTok, tTok, ctx, 0);
            return new EndpointDiffResult
            {
                Mode = DiffMode.Structural,
                Tree = tree,
                ChangeCount = tree.ChangeCount,
                Identical = tree.ChangeCount == 0,
                RawControl = controlRaw,
                RawTest = testRaw,
                ControlIsJson = true,
                TestIsJson = true,
            };
        }

        if (cOk ^ tOk)
        {
            // Exactly one side returned JSON — the other errored / was empty. That asymmetry is a finding.
            string missing = cOk ? "Test" : "Control";
            return new EndpointDiffResult
            {
                Mode = DiffMode.SingleSide,
                Identical = false,
                Notice = $"{missing} returned no JSON — showing each side's raw response.",
                RawControl = controlRaw,
                RawTest = testRaw,
                ControlIsJson = cOk,
                TestIsJson = tOk,
            };
        }

        // Neither side is JSON — compare the raw strings verbatim.
        bool equal = string.Equals(Normalize(controlRaw), Normalize(testRaw), StringComparison.Ordinal);
        return new EndpointDiffResult
        {
            Mode = DiffMode.BothNonJson,
            Identical = equal,
            Notice = equal
                ? "Both sides returned the same non-JSON response."
                : "Both sides returned non-JSON responses that differ.",
            RawControl = controlRaw,
            RawTest = testRaw,
            ControlIsJson = false,
            TestIsJson = false,
        };
    }

    /// <summary>Pretty-print a JSON string, or return it unchanged if it isn't JSON.</summary>
    public static string PrettyPrintOrRaw(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "[empty]";
        return TryParse(content, out var token) ? token.ToString(Formatting.Indented) : content;
    }

    // ─── Parsing ──────────────────────────────────────────────────────────

    public static bool TryParse(string? raw, out JToken token)
    {
        token = JValue.CreateNull();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (IsSentinel(raw)) return false;
        try
        {
            using var reader = new JsonTextReader(new StringReader(raw))
            {
                // JToken.Parse caps depth at 64 and would reject legitimately deep admin payloads as
                // "non-JSON". These come from our own trusted servers, so lift the cap; the diff's own
                // MaxDepth still bounds traversal. Keep date-like strings as strings so the diff never
                // normalizes timezones/formats out from under us.
                MaxDepth = null,
                DateParseHandling = DateParseHandling.None,
            };
            token = JToken.Load(reader, LoadSettings);

            // Reject trailing content after the first value (mirrors JToken.Parse).
            if (reader.Read() && reader.TokenType != JsonToken.None)
                return false;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsSentinel(string raw)
    {
        var s = raw.TrimStart();
        return s.StartsWith("[error", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("[empty]", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("[no response]", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? raw) => raw?.Trim() ?? "";

    // ─── Recursive structural compare ─────────────────────────────────────

    private static JsonDiffNode DiffToken(string key, JToken? control, JToken? test, Ctx ctx, int depth)
    {
        bool cAbsent = control is null;
        bool tAbsent = test is null;

        if (cAbsent && tAbsent)
            return new JsonDiffNode(key, JsonNodeKind.Value, DiffStatus.Same);
        if (cAbsent)
            return new JsonDiffNode(key, KindOf(test!), DiffStatus.TestOnly, control: null, test: test);
        if (tAbsent)
            return new JsonDiffNode(key, KindOf(control!), DiffStatus.ControlOnly, control: control, test: null);

        // Fast path: deep-equal subtrees collapse to Same with no children. DeepEquals is also
        // order-insensitive for object properties, which gives us key-order-insensitivity for free.
        if (JToken.DeepEquals(control, test))
            return new JsonDiffNode(key, KindOf(control!), DiffStatus.Same, control, test);

        if (depth >= ctx.Options.MaxDepth)
            return new JsonDiffNode(key, JsonNodeKind.Value, DiffStatus.Changed, control, test);

        bool bothObjects = control!.Type == JTokenType.Object && test!.Type == JTokenType.Object;
        bool bothArrays = control.Type == JTokenType.Array && test.Type == JTokenType.Array;

        if (bothObjects)
            return DiffObject(key, (JObject)control, (JObject)test, ctx, depth);
        if (bothArrays)
            return DiffArray(key, (JArray)control, (JArray)test, ctx, depth);

        // Two scalars, or a kind mismatch (object vs array vs scalar). Numerically-equal numbers
        // (1 vs 1.0) are treated as Same — DeepEquals reports those as different, but for a parity
        // tool that is just a serialization difference, not a real one.
        if (ScalarEquals(control, test))
            return new JsonDiffNode(key, JsonNodeKind.Value, DiffStatus.Same, control, test);

        return new JsonDiffNode(key, JsonNodeKind.Value, DiffStatus.Changed, control, test);
    }

    private static JsonDiffNode DiffObject(string key, JObject control, JObject test, Ctx ctx, int depth)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var p in control.Properties()) names.Add(p.Name);
        foreach (var p in test.Properties()) names.Add(p.Name);

        var children = new List<JsonDiffNode>(names.Count);
        foreach (var name in names)
        {
            var cVal = control[name];
            var tVal = test[name];

            if (ctx.Ignore.Contains(name))
            {
                // Forced Same — present but never counted. Prefer Control's value for display.
                children.Add(new JsonDiffNode(name, KindOf(cVal ?? tVal), DiffStatus.Same, cVal, tVal));
                continue;
            }

            children.Add(DiffToken(name, cVal, tVal, ctx, depth + 1));
        }

        var status = AnyChanged(children) ? DiffStatus.ContainerChanged : DiffStatus.Same;
        return new JsonDiffNode(key, JsonNodeKind.Object, status, control, test, children);
    }

    private static JsonDiffNode DiffArray(string key, JArray control, JArray test, Ctx ctx, int depth)
    {
        if (TryGetAlignmentKey(control, test, ctx.Options, out var idKey))
            return DiffArrayById(key, control, test, idKey, ctx, depth);

        if (AllScalars(control) && AllScalars(test))
            return DiffArrayMultiset(key, control, test);

        return DiffArrayByIndex(key, control, test, ctx, depth);
    }

    private static JsonDiffNode DiffArrayById(string key, JArray control, JArray test, string idKey, Ctx ctx, int depth)
    {
        var cMap = BuildIdMap(control, idKey);
        var tMap = BuildIdMap(test, idKey);

        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var k in cMap.Keys) ids.Add(k);
        foreach (var k in tMap.Keys) ids.Add(k);

        var children = new List<JsonDiffNode>(ids.Count);
        int rendered = 0;
        foreach (var id in ids)
        {
            if (rendered >= ctx.Options.MaxArrayChildren)
            {
                children.Add(Truncation(ids.Count - rendered));
                break;
            }
            cMap.TryGetValue(id, out var cEl);
            tMap.TryGetValue(id, out var tEl);
            children.Add(DiffToken($"[{idKey}={id}]", cEl, tEl, ctx, depth + 1));
            rendered++;
        }

        var status = AnyChanged(children) ? DiffStatus.ContainerChanged : DiffStatus.Same;
        return new JsonDiffNode(key, JsonNodeKind.Array, status, control, test, children, ArrayMatchMode.ById);
    }

    private static JsonDiffNode DiffArrayMultiset(string key, JArray control, JArray test)
    {
        var cCounts = CountScalars(control);
        var tCounts = CountScalars(test);

        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var k in cCounts.Keys) keys.Add(k);
        foreach (var k in tCounts.Keys) keys.Add(k);

        var children = new List<JsonDiffNode>();
        foreach (var k in keys)
        {
            cCounts.TryGetValue(k, out var c);
            tCounts.TryGetValue(k, out var t);
            var token = c.token ?? t.token!;
            int same = Math.Min(c.count, t.count);

            for (int i = 0; i < same; i++)
                children.Add(new JsonDiffNode("[=]", JsonNodeKind.Value, DiffStatus.Same, token, token));
            for (int i = 0; i < c.count - t.count; i++)
                children.Add(new JsonDiffNode("[-]", JsonNodeKind.Value, DiffStatus.ControlOnly, control: token));
            for (int i = 0; i < t.count - c.count; i++)
                children.Add(new JsonDiffNode("[+]", JsonNodeKind.Value, DiffStatus.TestOnly, test: token));
        }

        var status = AnyChanged(children) ? DiffStatus.ContainerChanged : DiffStatus.Same;
        return new JsonDiffNode(key, JsonNodeKind.Array, status, control, test, children, ArrayMatchMode.Multiset);
    }

    private static JsonDiffNode DiffArrayByIndex(string key, JArray control, JArray test, Ctx ctx, int depth)
    {
        int max = Math.Max(control.Count, test.Count);
        int cap = Math.Min(max, ctx.Options.MaxArrayChildren);

        var children = new List<JsonDiffNode>(cap);
        for (int i = 0; i < cap; i++)
        {
            var c = i < control.Count ? control[i] : null;
            var t = i < test.Count ? test[i] : null;
            children.Add(DiffToken($"[{i}]", c, t, ctx, depth + 1));
        }
        if (max > cap)
            children.Add(Truncation(max - cap));

        var status = AnyChanged(children) ? DiffStatus.ContainerChanged : DiffStatus.Same;
        return new JsonDiffNode(key, JsonNodeKind.Array, status, control, test, children, ArrayMatchMode.ByIndex);
    }

    // ─── Array helpers ────────────────────────────────────────────────────

    private static bool TryGetAlignmentKey(JArray control, JArray test, JsonDiffOptions options, out string idKey)
    {
        idKey = "";
        if (control.Count == 0 || test.Count == 0) return false;
        if (!AllObjects(control) || !AllObjects(test)) return false;

        foreach (var candidate in options.ArrayIdKeys)
        {
            if (UniqueOn(control, candidate) && UniqueOn(test, candidate))
            {
                idKey = candidate;
                return true;
            }
        }
        return false;
    }

    private static bool UniqueOn(JArray arr, string keyName)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var el in arr)
        {
            if (el is not JObject o) return false;
            var v = o[keyName];
            if (v is null || v.Type is JTokenType.Null or JTokenType.Object or JTokenType.Array) return false;
            if (!seen.Add(v.ToString())) return false; // duplicate id -> not usable
        }
        return true;
    }

    private static Dictionary<string, JObject> BuildIdMap(JArray arr, string idKey)
    {
        var map = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var el in arr)
        {
            var o = (JObject)el;
            map[o[idKey]!.ToString()] = o;
        }
        return map;
    }

    private static Dictionary<string, (JToken? token, int count)> CountScalars(JArray arr)
    {
        var counts = new Dictionary<string, (JToken? token, int count)>(StringComparer.Ordinal);
        foreach (var v in arr)
        {
            var k = CanonKey(v);
            counts[k] = counts.TryGetValue(k, out var e) ? (e.token, e.count + 1) : (v, 1);
        }
        return counts;
    }

    private static bool AllObjects(JArray arr)
    {
        foreach (var e in arr)
            if (e.Type != JTokenType.Object) return false;
        return true;
    }

    private static bool AllScalars(JArray arr)
    {
        foreach (var e in arr)
            if (e.Type is JTokenType.Object or JTokenType.Array) return false;
        return true;
    }

    private static bool AnyChanged(IReadOnlyList<JsonDiffNode> children)
    {
        for (int i = 0; i < children.Count; i++)
            if (children[i].Status != DiffStatus.Same) return true;
        return false;
    }

    private static JsonDiffNode Truncation(int remaining) =>
        new($"… {remaining} more element(s) not shown", JsonNodeKind.Value, DiffStatus.Same, isTruncation: true);

    private static JsonNodeKind KindOf(JToken? token) => token?.Type switch
    {
        JTokenType.Object => JsonNodeKind.Object,
        JTokenType.Array => JsonNodeKind.Array,
        _ => JsonNodeKind.Value,
    };

    private static bool ScalarEquals(JToken a, JToken b)
    {
        if (IsNumeric(a) && IsNumeric(b))
            return NumericCanon(a) == NumericCanon(b);
        return JToken.DeepEquals(a, b);
    }

    private static bool IsNumeric(JToken t) => t.Type is JTokenType.Integer or JTokenType.Float;

    // Canonical decimal string for a numeric token so that integer/float spellings of the same
    // value (1 vs 1.0) collapse, large integers (BigInteger-backed, i.e. > long.MaxValue) stay
    // exact, and nothing throws — Newtonsoft cannot convert a BigInteger via Value&lt;decimal&gt;()
    // or Value&lt;double&gt;(), so the old try/decimal/catch/double path threw out of Diff().
    private static string NumericCanon(JToken t)
    {
        var value = (t as JValue)?.Value;
        if (value is System.Numerics.BigInteger bi)
            return bi.ToString(CultureInfo.InvariantCulture);
        try
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                .ToString("0.############################", CultureInfo.InvariantCulture);
        }
        catch
        {
            // NaN / Infinity / doubles outside decimal range.
            return Convert.ToDouble(value, CultureInfo.InvariantCulture)
                .ToString("R", CultureInfo.InvariantCulture);
        }
    }

    // Bucket key for the multiset array compare. Numerically-equal scalars must share a key, so
    // numbers route through NumericCanon (1 and 1.0 collide); everything else is type + value.
    private static string CanonKey(JToken v) =>
        IsNumeric(v) ? "N:" + NumericCanon(v) : v.Type + ":" + v.ToString();

    // ─── Flattening for unified rendering ─────────────────────────────────

    /// <summary>
    /// Linearize a diff tree into render-ready lines. Each line carries its diff status and whether
    /// it belongs to a foldable run of unchanged context. Pretty-printing lives here (in Core) so the
    /// UI layer stays trivial and the output is unit-testable.
    /// </summary>
    public static IReadOnlyList<DiffLine> Flatten(JsonDiffNode root)
    {
        var lines = new List<DiffLine>();
        Emit(root, indent: 0, isRoot: true, lines);
        return lines;
    }

    private static void Emit(JsonDiffNode node, int indent, bool isRoot, List<DiffLine> lines)
    {
        if (node.IsTruncation)
        {
            lines.Add(Line(DiffStatus.Same, indent, node.Key, foldable: false, muted: true));
            return;
        }

        string label = LabelFor(node.Key, isRoot);
        switch (node.Status)
        {
            case DiffStatus.ControlOnly:
                EmitWholeToken(node.Control!, indent, label, DiffStatus.ControlOnly, lines);
                break;
            case DiffStatus.TestOnly:
                EmitWholeToken(node.Test!, indent, label, DiffStatus.TestOnly, lines);
                break;
            case DiffStatus.Changed:
                EmitWholeToken(node.Control!, indent, label, DiffStatus.ControlOnly, lines);
                EmitWholeToken(node.Test!, indent, label, DiffStatus.TestOnly, lines);
                break;
            case DiffStatus.Same:
                EmitSame(node, indent, label, lines, isRoot);
                break;
            case DiffStatus.ContainerChanged:
                EmitContainer(node, indent, label, lines, isRoot);
                break;
        }
    }

    private static void EmitSame(JsonDiffNode node, int indent, string label, List<DiffLine> lines, bool isRoot)
    {
        string value = node.Kind switch
        {
            JsonNodeKind.Object => "{…}",
            JsonNodeKind.Array => "[…]",
            _ => ScalarText(node.Control ?? node.Test),
        };
        lines.Add(Line(DiffStatus.Same, indent, label + value, foldable: !isRoot));
    }

    private static void EmitContainer(JsonDiffNode node, int indent, string label, List<DiffLine> lines, bool isRoot)
    {
        bool isArray = node.Kind == JsonNodeKind.Array;
        string open = isArray ? "[" : "{";
        string close = isArray ? "]" : "}";
        string hint = isArray && node.ArrayMode != ArrayMatchMode.None ? "   ← " + ModeHint(node.ArrayMode) : "";

        lines.Add(Line(DiffStatus.Same, indent, label + open + hint, foldable: false));
        foreach (var child in node.Children)
            Emit(child, indent + 1, isRoot: false, lines);
        lines.Add(Line(DiffStatus.Same, indent, close, foldable: false));
    }

    private static void EmitWholeToken(JToken token, int indent, string label, DiffStatus status, List<DiffLine> lines)
    {
        bool isContainer = token.Type is JTokenType.Object or JTokenType.Array;
        if (!isContainer)
        {
            lines.Add(Line(status, indent, label + ScalarText(token)));
            return;
        }

        var parts = token.ToString(Formatting.Indented).Replace("\r", "").Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            var content = (i == 0 ? label : "") + parts[i];
            lines.Add(Line(status, indent, content));
        }
    }

    private static DiffLine Line(DiffStatus status, int indent, string content, bool foldable = false, bool muted = false) =>
        new(Gutter(status) + new string(' ', indent * 2) + content, status, foldable, muted);

    private static string Gutter(DiffStatus s) => s switch
    {
        DiffStatus.ControlOnly => "- ",
        DiffStatus.TestOnly => "+ ",
        _ => "  ",
    };

    private static string LabelFor(string key, bool isRoot)
    {
        if (isRoot || key == "$") return "";
        if (key is "[=]" or "[+]" or "[-]") return "";   // multiset element — value only
        if (key.StartsWith('[')) return key + " ";        // array marker, e.g. [id=123] or [3]
        return "\"" + key + "\": ";                        // object property
    }

    private static string ScalarText(JToken? t) => t is null ? "null" : t.ToString(Formatting.None);

    private static string ModeHint(ArrayMatchMode m) => m switch
    {
        ArrayMatchMode.ById => "by-id",
        ArrayMatchMode.Multiset => "multiset",
        ArrayMatchMode.ByIndex => "by-index",
        _ => "",
    };
}
