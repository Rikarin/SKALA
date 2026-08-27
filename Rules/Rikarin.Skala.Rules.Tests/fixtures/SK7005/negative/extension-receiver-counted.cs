// ⚠ "A `this` parameter on an extension method is counted, because a caller supplies it" —
// rules.json, SK7005. Seven declared plus the receiver is eight, which is the threshold and not over
// it: the fixture proves the receiver counts by sitting exactly on the boundary it would cross.
public static class ExtensionReceiverCounted {
    public static int Configure(this string subject, int a, int b, int c, int d, int e, int f, int g) =>
        subject.Length + a + b + c + d + e + f + g;
}
