public sealed class Reader {
    public bool CanRead(string text) {
        int number;
        return int.TryParse(text, out number);
    }
}
