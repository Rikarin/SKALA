// ⚠ A struct's own override of `ToString` is called on the capture without boxing, so one that
// mutates writes the capture today and a copy after `readonly` (#412). The inherited
// `object.ToString` boxes either way and is not the same case.
public struct Noisy {
    public int Formatted;

    public override string ToString() {
        Formatted++;
        return "noisy";
    }
}

struct Labelled(Noisy value) {
    public string Label() => value.ToString();

    public int Formatted => value.Formatted;
}

public static class Probe {
    public static int Run() {
        var labelled = new Labelled(new Noisy());
        labelled.Label();
        return labelled.Formatted;
    }
}
