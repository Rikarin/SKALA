// ⚠ An unconstrained `T` capture reaches a struct's own override of an `object` member through a
// constrained call, so an override that mutates is a write to the capture today (#412).
public struct Noisy {
    public int Formatted;

    public override string ToString() {
        Formatted++;
        return "noisy";
    }
}

struct Labelled<T>(T value) {
    public string Label() => value!.ToString() ?? "";

    public T Value => value;
}

public static class Probe {
    public static int Run() {
        var labelled = new Labelled<Noisy>(new Noisy());
        labelled.Label();
        return labelled.Value.Formatted;
    }
}
