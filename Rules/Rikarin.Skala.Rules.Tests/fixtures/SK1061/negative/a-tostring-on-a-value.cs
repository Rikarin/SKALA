// The receiver is a variable, not a constant member: its name is not its value.
public enum Colour {
    Red,
    Green
}

public sealed class Renderer {
    public string Label(Colour colour) => colour.ToString();
}
