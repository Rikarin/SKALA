public sealed class Matching {
    static void Emit(string value) { }

    public static void Handle(object candidate) {
        if (candidate is string text) {
            if (text.Length > 2) {
                Emit(text);
            }
        }
    }
}
