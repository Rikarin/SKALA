// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-09
// #595 and #596: an `=` whose value cannot end the line in time beside it breaks — a lambda without parentheses
// whose `=>` runs past the margin, and a conditional whose condition's call opens past the margin.

class C595 {
    void A() {
        Func<IReadOnlyDictionary<Memory<int>, Memory<int>>, IReadOnlyDictionary<Memory<int>, Memory<int>>> v13 =
            static x => state;
        Func<IReadOnlyDictionary<Memory<int>, Memory<int>>, IReadOnlyDictionary<Memory<int>, Memory<int>>> v1333333 =
            static x => state;
        Func<IReadOnlyDictionary<Memory<int>, Memory<int>>, IReadOnlyDictionary<Memory<int>, Memory<int>>> v1333333 =
            x => state;
        Func<IReadOnlyDictionary<Memory<int>, Memory<int>>, IReadOnlyDictionary<Memory<int>, Memory<int>>> v13 = x =>
            state;
    }

    void B() {
        Dictionary<(decimal First, ValueTask<string> Second), List<(decimal First, ValueTask<string> Second)>> v19 =
            Select(
                x20 => Convert<string>(x21 => false, name22: 64774),
                ((x, y) => { return "sss"; }),
                x23 => ((x, y) => null),
                $"at {value:F2}"
            )
                ? Cast<object>(
                    in Convert<Span<List<decimal>>, Task<(DateTime First, DateTime Second)>>(),
                    ("ss" + false || null & 35597),
                    new string(1.0m, "ss") { P24 = "ss", P25 = "ss", P26 = 23929 }
                )
                : context;
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Select(aaaa, bbbb) ? Cast<object>(first, second) : context;
    }
}
