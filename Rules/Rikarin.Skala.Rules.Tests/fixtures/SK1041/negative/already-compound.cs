public sealed class Done {
    int count;
    string text = string.Empty;

    public void Advance(string suffix) {
        count += 1;
        text += suffix;
    }
}
