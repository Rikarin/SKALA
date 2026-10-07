// ⚠ #412's audit: a pattern label sharing `default:`'s section is tried before the later `case 5:`.
// Deleting it sends 5 to "five".
public static class Probe {
    static string Classify(int value) {
        switch (value) {
            case int n when n > 0:
            default:
                return "first";
            case 5:
                return "five";
        }
    }

    public static string Run() => Classify(5);
}
