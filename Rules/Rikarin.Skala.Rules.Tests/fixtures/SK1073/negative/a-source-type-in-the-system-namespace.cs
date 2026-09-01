namespace System {
    // ⚠ It carries an `Empty` of its own type, so the member lookup succeeds and only the
    // "this came from a referenced assembly" test stands between the rule and a rewrite to a
    // static field that is null.
    public sealed class Guid {
        public static readonly Guid Empty = null!;
    }

    public sealed class Registry {
        public Guid Fresh() => new Guid();
    }
}
