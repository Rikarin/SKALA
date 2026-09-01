public sealed class Paths {
    public static bool Mentions(string text, int start) => text.Substring(start).IndexOf(Needle()) >= 0;

    static string Needle() => "x";
}
