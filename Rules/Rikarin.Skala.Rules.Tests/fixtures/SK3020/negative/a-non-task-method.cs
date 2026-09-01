public sealed class Loader {
    // Nothing awaits a string; returning null is an ordinary design decision.
    public string? Read(bool cached) {
        if (cached) {
            return null;
        }

        return "value";
    }
}
