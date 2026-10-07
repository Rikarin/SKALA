// ⚠ #412's audit: `case` labels are tried in source order and `default:` last, so `case 5:` is what
// keeps 5 out of `case > 4:`. Without it 5 reaches the later section.
public static class Probe {
    static string Classify(int value) {
        switch (value) {
            case 5:
            default:
                return "default";
            case > 4:
                return "big";
        }
    }

    public static string Run() => Classify(5);
}
