public interface IRegistry<T> {
    T Get();

    void Register<U>(U value) where U : T;
}
