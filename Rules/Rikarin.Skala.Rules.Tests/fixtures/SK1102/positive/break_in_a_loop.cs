public sealed class Scanning {
    public static int Scan(int limit) {
        var seen = 0;
        while (seen < limit) {
            seen = Step(seen);

            int Step(int value) => value + 1;

            break;
        }

        return seen;
    }
}
