public sealed class WiderAssignmentTarget {
    object? stored;

    // The left side of the assignment is `object?`, not `int?`, so the position does not write the
    // nullable type down.
    public void Set(int value) {
        stored = new int?(value);
    }

    public bool HasValue() => stored is not null;
}
