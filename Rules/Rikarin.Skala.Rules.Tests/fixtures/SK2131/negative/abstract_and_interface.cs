// An abstract or interface property has no storage of its own; the implementing type decides.
//
// ⚠ `Assigned` is here because a sabotage stayed green without it. The rule carries the
// `IsAbstract` test twice — once as a cheap symbol-level pre-filter that decides whether the type
// is worth walking at all, and once per property — and with only abstract properties in the file
// the pre-filter answered first, so breaking the per-property test changed nothing. A concrete
// get-only property in the same type makes the pre-filter pass, which is what puts the second test
// on the hook.
interface IWindow {
    int Width { get; }
}

abstract class WindowBase {
    public abstract int Height { get; }

    public int Assigned { get; }

    protected WindowBase(int assigned) => Assigned = assigned;
}
