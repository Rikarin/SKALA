class Pair {
    public int First { get; set; }

    public void Deconstruct(out int first, out int second) {
        first = First;
        second = 0;
    }
}

class Positional {
    public bool Interesting(Pair p) => p is (1, _) or { First: 2 };
}
