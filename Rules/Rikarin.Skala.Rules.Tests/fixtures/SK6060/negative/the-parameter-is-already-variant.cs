public interface IAlreadyCovariant<out T> {
    T Create();
}

public interface IAlreadyContravariant<in T> {
    void Accept(T value);
}
