// `typeof(T).Name` is the *argument's* name at run time; `nameof(T)` is the literal "T".
public sealed class Reflector<T> {
    public string TypeName() => typeof(T).Name;
}
