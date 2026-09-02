public sealed class Label {
    readonly string text = "unset";

    public Label(string given) => this.text = given;

    public string Text => text;
}
