public interface IFactory<T> {
    T Create();
}

public sealed class StringFactory : IFactory<string> {
    public string Create() => string.Empty;
}
