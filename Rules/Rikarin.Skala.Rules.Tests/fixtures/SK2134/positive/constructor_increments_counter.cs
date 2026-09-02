// ⚠ Pinned positive on purpose. A counter bumped from a constructor is the canonical shape of this
// concept, not a false positive: it is shared mutable state, `++` is not atomic, and two threads
// constructing at once lose an increment. This fixture exists so that nobody later mistakes it for
// noise and adds an exclusion for it.
sealed class Widget {
    static int created;

    public Widget() {
        created++;
    }

    public static int Created => created;
}
