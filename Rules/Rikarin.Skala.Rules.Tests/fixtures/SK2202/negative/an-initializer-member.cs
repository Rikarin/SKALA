using System;

// ⚠ An object, collection or `with` initializer member parses as a `SimpleAssignmentExpression`,
// exactly like a real assignment, and it modifies nothing: it names a member of an object being
// constructed on the line. This is the false-positive class the Vixen measurement found — nine of
// its ten findings were this shape.
public sealed record Bound(int Slot) {
    public string Effect { get; init; } = "";
}

public sealed class Renderer {
    public void Bind(Action<Bound>? onBind, string effect) {
        onBind?.Invoke(new Bound(1) { Effect = effect });
    }

    public void Rebind(Action<Bound>? onBind, Bound existing, string effect) {
        onBind?.Invoke(existing with { Effect = effect });
    }

    public void Fill(Action<System.Collections.Generic.Dictionary<int, string>>? sink, string value) {
        sink?.Invoke(new System.Collections.Generic.Dictionary<int, string> { [0] = value });
    }
}
