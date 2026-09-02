using System.Diagnostics;

// A source type's `Remove` is never matched: the rule cannot know what it does, and guessing is the
// thing it exists not to do.
public sealed class Registry {
    public bool Remove(int id) => id > 0;
}

public sealed class Tracker {
    readonly Registry registry = new();

    public void Complete(int id) {
        Debug.Assert(registry.Remove(id));
    }
}
