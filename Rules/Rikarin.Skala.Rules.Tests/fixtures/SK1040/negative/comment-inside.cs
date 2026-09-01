using System;

// The rewrite replaces the whole type name, so a comment written inside it is text the fix would
// silently delete.
public sealed class Annotated {
    public Nullable</* the running total */ int> Total { get; init; }
}
