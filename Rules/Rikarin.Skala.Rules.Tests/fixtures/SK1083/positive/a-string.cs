public sealed class Registry {
    public static int Digits(string text) {
        var seen = 0;
        for (var i = 0; i < text.Length; i++) {
            if (char.IsDigit(text[i])) {
                seen++;
            }
        }

        return seen;
    }
}
