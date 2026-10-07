// ⚠ A struct field of a struct capture is the capture's own storage, so a mutating call through the
// chain mutates the capture today and a copy after `readonly` (#412).
public struct Counter {
    public int Count;

    public void Increment() => Count++;
}

public struct Pair {
    public Counter Left;
}

struct Tally(Pair pair) {
    public void Bump() => pair.Left.Increment();

    public int Count => pair.Left.Count;
}

public static class Probe {
    public static int Run() {
        var tally = new Tally(new Pair());
        tally.Bump();
        return tally.Count;
    }
}
