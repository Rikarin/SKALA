public sealed class Builder {
    string text = string.Empty;

    public void Append(string suffix) {
        text = text + suffix;
    }

    public override string ToString() => text;
}
