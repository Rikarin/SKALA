class Pair {
    public int First { get; set; }

    public void Deconstruct(out int first, out int second) {
        first = First;
        second = 0;
    }
}

// ⚠ A recursive pattern may carry a positional clause AND a property clause at once, and that is
// the shape that REACHES the positional guard. Written as `p is (1, _) or { First: 2 }` the guard
// is unreachable: a bare positional pattern has no property clause, so the "exactly one property
// subpattern" requirement declines it first and removing the positional guard turns nothing red.
//
// Merging this would silently drop the `(1, _)` test.
class Positional {
    public bool Interesting(Pair p) => p is (1, _) { First: 2 } or { First: 3 };
}
