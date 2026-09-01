using System;

// ⚠ `capacity` belongs to `Outer`'s primary constructor and is not in scope inside `Inner`. If the
// walk out of the throw did not stop at the containing type it would offer `nameof(capacity)` here,
// which does not compile — the one way this rule could break a build on its own advice.
public sealed class Outer(int capacity) {
    public int Capacity => capacity;

    public sealed class Inner {
        public void Fill(int size) {
            if (size < 0) {
                throw new ArgumentOutOfRangeException("capaciy", size, "must not be negative");
            }
        }
    }
}
