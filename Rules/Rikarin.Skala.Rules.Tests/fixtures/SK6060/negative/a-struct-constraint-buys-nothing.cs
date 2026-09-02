// `interface I<out T> where T : struct` compiles, and is worth nothing: variance exists to admit
// reference conversions and a value type admits none.
public interface IValueFactory<T> where T : struct {
    T Create();
}

public interface IUnmanagedFactory<T> where T : unmanaged {
    T Create();
}
