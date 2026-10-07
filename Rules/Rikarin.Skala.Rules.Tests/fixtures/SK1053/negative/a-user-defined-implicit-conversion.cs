// `Handle handle = Open();` runs `op_Implicit`; `_ = Open();` would not, so the fix would compile
// and silently delete a call.
public readonly struct Token;

public sealed class Handle {
    public static implicit operator Handle(Token token) => new();
}

public sealed class Pool {
    static Token Open() => default;

    public void Warm() {
        Handle handle = Open();
    }
}
