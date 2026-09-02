public sealed class Journal {
    object gate;

    int entries;

    public Journal() {
        // ⚠ The most important negative of shape 2, and the one that decides whether the rule is
        // usable. A non-`readonly` field assigned once in a constructor is effectively `readonly` —
        // it is common, it is correct, and reporting it would make this rule noise. Nothing about
        // the field's modifiers says so; only the location of every write does.
        gate = new object();
    }

    public void Append() {
        lock (gate) {
            entries++;
        }
    }
}
