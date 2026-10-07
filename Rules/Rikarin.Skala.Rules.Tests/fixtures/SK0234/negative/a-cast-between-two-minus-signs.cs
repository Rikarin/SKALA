// ⚠ #424: deleting `(int)` from `a-(int)-b` leaves `a--b`, a post-decrement followed by a stray
// `b` — CS1002 (#412's audit). A cast is a token boundary as well as a conversion.
public static class Probe {
    public static int Run() {
        int a = 5, b = 2;
        int x = a-(int)-b;
        return x;
    }
}
