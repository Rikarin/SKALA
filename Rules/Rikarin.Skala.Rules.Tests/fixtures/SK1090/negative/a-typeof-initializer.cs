// `typeof` folds to the same instance at run time but is not a compile-time constant, and the
// rule's predicate is Roslyn's constant folding rather than a hand-written shape list.
public sealed class Kinds {
    public System.Type Kind { get; } = typeof(int);
}
