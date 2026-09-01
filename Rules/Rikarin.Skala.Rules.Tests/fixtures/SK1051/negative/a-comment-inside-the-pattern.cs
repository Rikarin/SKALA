public sealed class Gate {
    public bool Open(int value) =>
        value is not
            // Deliberately doubled while the third condition is being written.
            not > 0;
}
