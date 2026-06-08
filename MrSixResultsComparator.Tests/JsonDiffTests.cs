using MrSixResultsComparator.Core.Diffing;
using Xunit;

namespace MrSixResultsComparator.Tests;

public class JsonDiffTests
{
    private static EndpointDiffResult D(string control, string test) => JsonDiff.Diff(control, test);

    // ─── Object key order-insensitivity & scalar equality ─────────────────

    [Fact]
    public void KeyReorder_IsIdentical()
    {
        var r = D("{\"a\":1,\"b\":2}", "{\"b\":2,\"a\":1}");
        Assert.Equal(DiffMode.Structural, r.Mode);
        Assert.True(r.Identical);
        Assert.Equal(0, r.ChangeCount);
    }

    [Fact]
    public void IntVsFloat_IsSame()
    {
        Assert.True(D("{\"x\":1}", "{\"x\":1.0}").Identical);
    }

    [Fact]
    public void IntVsStringOfSameDigits_IsChanged()
    {
        var r = D("{\"x\":123}", "{\"x\":\"123\"}");
        Assert.False(r.Identical);
        Assert.Equal(1, r.ChangeCount);
    }

    [Fact]
    public void AddedField_CountsAsOne()
    {
        Assert.Equal(1, D("{\"a\":1}", "{\"a\":1,\"b\":2}").ChangeCount);
    }

    [Fact]
    public void RemovedField_CountsAsOne()
    {
        Assert.Equal(1, D("{\"a\":1,\"b\":2}", "{\"a\":1}").ChangeCount);
    }

    [Fact]
    public void NullVsValue_IsChanged()
    {
        Assert.Equal(1, D("{\"a\":null}", "{\"a\":5}").ChangeCount);
    }

    [Fact]
    public void NullVsNull_IsSame()
    {
        Assert.True(D("{\"a\":null}", "{\"a\":null}").Identical);
    }

    [Fact]
    public void NestedChange_CountsLeafOnly()
    {
        var r = D("{\"o\":{\"a\":1,\"b\":2}}", "{\"o\":{\"a\":9,\"b\":2}}");
        Assert.Equal(1, r.ChangeCount);
    }

    // ─── Arrays ───────────────────────────────────────────────────────────

    [Fact]
    public void ScalarArrayReorder_IsSame()
    {
        Assert.True(D("[1,2,3]", "[3,2,1]").Identical);
    }

    [Fact]
    public void ScalarArrayAddRemove_CountsBoth()
    {
        // control has 1 (not in test), test has 4 (not in control) => 2 changes
        Assert.Equal(2, D("[1,2,3]", "[2,3,4]").ChangeCount);
    }

    [Fact]
    public void ScalarArrayDuplicates_AreToleratedAsMultiset()
    {
        // control: two 1s; test: one 1 => one ControlOnly
        Assert.Equal(1, D("[1,1,2]", "[1,2]").ChangeCount);
    }

    [Fact]
    public void ObjectArrayReorderById_IsSame()
    {
        var c = "[{\"id\":1,\"v\":\"a\"},{\"id\":2,\"v\":\"b\"}]";
        var t = "[{\"id\":2,\"v\":\"b\"},{\"id\":1,\"v\":\"a\"}]";
        Assert.True(D(c, t).Identical);
    }

    [Fact]
    public void ObjectArrayFieldChange_CountsAsOne()
    {
        var c = "[{\"id\":1,\"v\":\"a\"}]";
        var t = "[{\"id\":1,\"v\":\"b\"}]";
        Assert.Equal(1, D(c, t).ChangeCount);
    }

    [Fact]
    public void ObjectArrayAddedElement_CountsAsOne()
    {
        Assert.Equal(1, D("[{\"id\":1}]", "[{\"id\":1},{\"id\":2}]").ChangeCount);
    }

    [Fact]
    public void ObjectArrayDuplicateIds_FallBackToIndexWise()
    {
        // duplicate id disables id-alignment; index-wise compares position-for-position.
        // Reordered here, so each position's "v" differs => 2 changes (the documented noisy fallback).
        var c = "[{\"id\":1,\"v\":\"a\"},{\"id\":1,\"v\":\"b\"}]";
        var t = "[{\"id\":1,\"v\":\"b\"},{\"id\":1,\"v\":\"a\"}]";
        Assert.Equal(2, D(c, t).ChangeCount);
    }

    [Fact]
    public void EmptyArrayVsObject_IsChanged_NoThrow()
    {
        Assert.False(D("[]", "{}").Identical);
    }

    // ─── Error / non-JSON handling ────────────────────────────────────────

    [Fact]
    public void OneSideError_IsSingleSide()
    {
        var r = D("[error: HTTP 500 for /admin/getBlocks]", "{\"a\":1}");
        Assert.Equal(DiffMode.SingleSide, r.Mode);
        Assert.False(r.Identical);
        Assert.True(r.TestIsJson);
        Assert.False(r.ControlIsJson);
    }

    [Fact]
    public void BothSameError_IsIdentical()
    {
        var r = D("[error: HTTP 500]", "[error: HTTP 500]");
        Assert.Equal(DiffMode.BothNonJson, r.Mode);
        Assert.True(r.Identical);
    }

    [Fact]
    public void BothDifferentErrors_AreNotIdentical()
    {
        var r = D("[error: HTTP 500]", "[error: HTTP 404]");
        Assert.Equal(DiffMode.BothNonJson, r.Mode);
        Assert.False(r.Identical);
    }

    [Fact]
    public void EmptySentinel_IsNonJson()
    {
        Assert.Equal(DiffMode.SingleSide, D("[empty]", "{\"a\":1}").Mode);
    }

    // ─── Ignore list ──────────────────────────────────────────────────────

    [Fact]
    public void IgnoredKey_IsSuppressed()
    {
        var opts = new JsonDiffOptions { IgnoreKeys = new[] { "ts" } };
        var r = JsonDiff.Diff("{\"a\":1,\"ts\":100}", "{\"a\":1,\"ts\":200}", opts);
        Assert.True(r.Identical);
    }

    [Fact]
    public void IgnoredKey_NestedIsSuppressed()
    {
        var opts = new JsonDiffOptions { IgnoreKeys = new[] { "updatedAt" } };
        var r = JsonDiff.Diff(
            "{\"o\":{\"a\":1,\"updatedAt\":\"x\"}}",
            "{\"o\":{\"a\":1,\"updatedAt\":\"y\"}}", opts);
        Assert.True(r.Identical);
    }

    [Fact]
    public void DefaultOptions_SuppressNothing()
    {
        // default is "hide nothing" — a timestamp diff is a real diff
        Assert.False(D("{\"a\":1,\"ts\":100}", "{\"a\":1,\"ts\":200}").Identical);
    }

    // ─── Flatten rendering ────────────────────────────────────────────────

    [Fact]
    public void Flatten_ChangedScalar_EmitsRemovedAndAddedLines()
    {
        var r = D("{\"tier\":\"GOLD\"}", "{\"tier\":\"PLATINUM\"}");
        var lines = JsonDiff.Flatten(r.Tree!);

        Assert.Contains(lines, l => l.Status == DiffStatus.ControlOnly && l.Text.Contains("GOLD"));
        Assert.Contains(lines, l => l.Status == DiffStatus.TestOnly && l.Text.Contains("PLATINUM"));
    }

    [Fact]
    public void Flatten_UnchangedFields_AreFoldable()
    {
        var r = D("{\"a\":1,\"b\":2,\"c\":3,\"d\":9}", "{\"a\":1,\"b\":2,\"c\":3,\"d\":8}");
        var lines = JsonDiff.Flatten(r.Tree!);
        Assert.Contains(lines, l => l.Foldable); // a, b, c are unchanged context
    }

    [Fact]
    public void Flatten_StringWithMarkupChars_IsNotInterpretedAsContainer()
    {
        // ensures the value is rendered as a single scalar line (the UI HTML-encodes it)
        var r = D("{\"x\":\"<b>&amp;</b>\"}", "{\"x\":\"y\"}");
        var lines = JsonDiff.Flatten(r.Tree!);
        Assert.Contains(lines, l => l.Text.Contains("<b>"));
    }

    // ─── Numeric edge cases (regressions from the adversarial review) ──────

    [Fact]
    public void MultisetNumericEquivalence_1vs1dot0_IsSame()
    {
        // In multiset mode, 1 (Integer) and 1.0 (Float) must collide on a numeric-canonical key.
        var r = D("[1, 1.0]", "[1, 1]");
        Assert.Equal(0, r.ChangeCount);
    }

    [Fact]
    public void LargeIntegers_BeyondLong_DoNotThrow()
    {
        // Values > long.MaxValue parse as BigInteger; the engine must compare them without throwing.
        var same = D("{\"id\":18446744073709551616}", "{\"id\":18446744073709551616}");
        Assert.True(same.Identical);

        var diff = D("{\"id\":18446744073709551616}", "{\"id\":18446744073709551617}");
        Assert.Equal(DiffMode.Structural, diff.Mode);
        Assert.Equal(1, diff.ChangeCount);
    }

    [Fact]
    public void LargeIntegersInScalarArray_DoNotThrow()
    {
        // Same BigInteger trigger, but through the multiset path.
        var r = D("[18446744073709551616, 1]", "[18446744073709551617, 1]");
        Assert.Equal(DiffMode.Structural, r.Mode);
        Assert.Equal(2, r.ChangeCount); // one ControlOnly + one TestOnly
    }

    // ─── Truncation / depth guards ────────────────────────────────────────

    [Fact]
    public void MaxDepth_StopsRecursion_StillReportsAChange()
    {
        var deep = "{\"a\":" + string.Concat(Enumerable.Repeat("{\"b\":", 70)) + "1" + new string('}', 70) + "}";
        var deepChanged = "{\"a\":" + string.Concat(Enumerable.Repeat("{\"b\":", 70)) + "2" + new string('}', 70) + "}";
        var r = D(deep, deepChanged);
        // Recursion stops at MaxDepth and emits a Changed leaf rather than deep-comparing.
        Assert.True(r.ChangeCount >= 1);
    }

    [Fact]
    public void IndexWiseArray_TruncatesAtMaxArrayChildren()
    {
        // Objects without an id-like field force the index-wise path, which caps at MaxArrayChildren (500).
        var arr1 = "[" + string.Join(",", Enumerable.Range(1, 600).Select(i => $"{{\"v\":{i}}}")) + "]";
        var arr2 = "[" + string.Join(",", Enumerable.Range(1, 600).Select(i => $"{{\"v\":{i + 1000}}}")) + "]";
        var r = D(arr1, arr2);
        // 500 element-objects compared (each one change); the truncation marker is Same and uncounted.
        Assert.Equal(500, r.ChangeCount);
    }

    // ─── Multiset null-side rendering (Flatten must not deref the absent side) ─

    [Fact]
    public void Flatten_MultisetTestOnly_DoesNotCrash()
    {
        var r = D("[1,1]", "[1,2,2]");
        Assert.NotEmpty(JsonDiff.Flatten(r.Tree!));
    }

    [Fact]
    public void Flatten_MultisetControlOnly_DoesNotCrash()
    {
        var r = D("[1,2,2]", "[1,1]");
        Assert.NotEmpty(JsonDiff.Flatten(r.Tree!));
    }
}
