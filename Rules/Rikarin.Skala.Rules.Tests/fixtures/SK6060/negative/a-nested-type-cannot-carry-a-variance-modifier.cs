// CS8427: enums, classes and structures cannot be declared in an interface that has an `in` or
// `out` type parameter, so the modifier is illegal here whatever the signatures say.
public interface IWithNestedClass<T> {
    T Create();

    sealed class Holder {
        public T? Value;
    }
}

// A nested delegate is legal and is variance-checked through its own Invoke, which is not a member
// of this interface and is not walked. One guard declines both.
public interface IWithNestedDelegate<T> {
    T Create();

    delegate void Handler(T value);
}
