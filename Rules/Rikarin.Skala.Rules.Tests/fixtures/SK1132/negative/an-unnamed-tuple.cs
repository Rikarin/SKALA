// `Item1` is the only name an unnamed tuple's element has.
public static class Pairs {
    public static int Sum() {
        var t = (1, 2);
        return t.Item1 + t.Item2;
    }
}
