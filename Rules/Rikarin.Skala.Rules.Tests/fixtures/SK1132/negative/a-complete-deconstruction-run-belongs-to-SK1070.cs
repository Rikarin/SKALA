// ⚠ The one place SK1070 and this rule meet: every element of a named tuple read once, in order, by
// consecutive `var` declarations. SK1070 replaces the lines with `var (low, high, step) = bounds;`, and a
// rename inside them would race it — renaming first turns the reads into `bounds.Low`, which SK1070 no
// longer recognises, and the deconstruction is lost. The run is SK1070's.
public sealed class Measurement {
    public int Range() {
        var bounds = Bounds();
        var low = bounds.Item1;
        var high = bounds.Item2;
        var step = bounds.Item3;
        return (high - low) / step;
    }

    static (int Low, int High, int Step) Bounds() => (0, 10, 2);
}
