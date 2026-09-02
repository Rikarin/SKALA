public interface ISink<T> {
    void Accept(T value);

    void AcceptAll(System.Collections.Generic.IEnumerable<T> values);
}
