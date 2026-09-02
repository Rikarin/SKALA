// ⚠ #302's reproduction, and the shape no fixture in this repository had. The guard walked
// `declaration.DescendantTrivia()`, which covers the node's FULL span and therefore the doc comment
// written ABOVE it — text no fix here deletes, since the rewrite replaces the type name alone. So a
// documented field silently declined while the identical undocumented one fired, and in a documented
// codebase that is nearly every member.
//
// ⚠ It failed in the direction that looks clean: the rule's negatives all still passed, because a
// rule that never fires declines everything it is supposed to decline.
class C {
    /// <summary>The gate this type synchronises on.</summary>
    static readonly object gate = new();

    static void M() {
        lock (C.gate) { }
    }
}
