public sealed class Bounded {
    static bool Advance() => false;

    public static int Take(int limit) {
        var taken = 0;
        for (; taken < limit && Advance();) {
            taken++;
        }

        return taken;
    }
}
