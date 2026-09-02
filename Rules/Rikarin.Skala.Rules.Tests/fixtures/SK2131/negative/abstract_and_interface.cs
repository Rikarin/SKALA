// An abstract or interface property has no storage of its own; the implementing type decides.
interface IWindow {
    int Width { get; }
}

abstract class WindowBase {
    public abstract int Height { get; }
}
