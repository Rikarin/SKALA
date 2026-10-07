// ⚠ #423: C# 14 lets a type declare an instance `operator +=`. `a += 1` binds it and `a = a + 1`
// binds the static `operator +`, so the two forms run different methods: the alias below is left
// untouched before the fix and mutated after it (#412's audit).
public sealed class Accumulator {
    public int Value;

    public Accumulator(int value) => Value = value;

    public static Accumulator operator +(Accumulator a, int b) => new Accumulator(a.Value + b);

    public void operator +=(int b) => Value += b * 100;
}

public static class Probe {
    public static int Run() {
        var a = new Accumulator(1);
        var alias = a;
        a = a + 1;
        return alias.Value;
    }
}
