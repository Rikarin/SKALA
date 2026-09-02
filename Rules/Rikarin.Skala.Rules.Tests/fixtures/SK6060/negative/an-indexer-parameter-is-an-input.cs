public interface ILookup<T> {
    T Get(int index);

    int this[T key] { get; }
}
