// A setter that transforms `value` is not `field = value`, so it is outside the shape. What it
// writes is a decision the author took in front of the reader.
sealed class Label {
    string text = "";
    string trimmed = "";

    public string Text {
        get => text;
        set => trimmed = value.Trim();
    }

    public string Trimmed => trimmed;
}
