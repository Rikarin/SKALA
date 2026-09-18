// The gap on either side of a range's `..`, and after a spread's, is the first gap the fuzzer's
// absorption property tripped over: no key in the export governs it, so the oracle keeps the author's
// zero or one space and collapses a run, and `SpaceRules.Ungoverned` answers it with `Preserve`. Until
// #376 the oracle's answer for it lived in a code remark and docs/plan/12, not in a fixture — the
// range files in the corpus write each shape one way. Every shape here is written twice, closed and
// spaced, and once as a two-space run, so the fixture answers "is the author's gap kept" in both
// directions for a range with both operands, a range with one, and a spread element. The slice
// patterns at the end are the control: `space_within_slice_pattern` governs that `..`, and the oracle
// inserts the space into `..var rest`.
class RangeOperatorGap {
    int[] Closed(int[] a) => a[1..3];

    int[] Spaced(int[] a) => a[1 .. 3];

    int[] Run(int[] a) => a[1  ..  3];

    int[] Mixed(int[] a) => a[1.. 3];

    int[] LeftOnly(int[] a) => a[1..];

    int[] LeftOnlySpaced(int[] a) => a[1 ..];

    int[] RightOnly(int[] a) => a[..^1];

    int[] RightOnlySpaced(int[] a) => a[.. ^1];

    int[] Spread(int[] a) => [0, ..a, 4];

    int[] SpreadSpaced(int[] a) => [0, .. a, 4];

    int[] SpreadRun(int[] a) => [0, ..   a, 4];

    bool Slice(int[] a) => a is [1, ..var rest] && rest.Length > 0;

    bool SliceSpaced(int[] a) => a is [1, .. var rest] && rest.Length > 0;
}
