// ⚠ A get-only auto-property is assignable from the declaring type's constructors, and a
// computed one is not assignable at all: the fix would be CS0200.
public sealed class Overridden {
    public int Maximum { get; } = 1;

    public Overridden(bool wide) {
        Maximum = wide ? 100 : 1;
    }
}
