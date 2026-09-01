public enum Colour {
    Red,
    Green
}

public sealed class Palette {
    public string Label() => Colour.Red.ToString();
}
