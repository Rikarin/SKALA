// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// #432 and SK-DIV-0187: an argument a `[CallerArgumentExpression]` parameter captures is a string the
// program prints, whitespace and line breaks included, so Skala leaves it byte-identical and the oracle
// formats it. Every captured argument below is mis-spaced or mis-wrapped on purpose; every divergent
// line in this file is that decision, and the ones that agree — the gaps around a captured argument,
// and arguments nothing captures — are the half of it that is still formatted. The corpus had 222
// captured arguments, every one a single identifier, so it could not measure this at all.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

public delegate string Checker(int value, [CallerArgumentExpression("value")] string text = "");

public static class Captures {
    public static string Check(bool ok, [CallerArgumentExpression(nameof(ok))] string text = "") => text;

    public static string Plain(bool ok) => ok.ToString();

    public static string Text<T>(this T value, [CallerArgumentExpression("value")] string text = "") => text;

    public static void Run(int a, int b, Checker check, object? item) {
        Check(a < b);
        Check(a < b);
        Check(a < b);
        Check(a < b);
        Check((a < b));
        Check(a < b, "explicit" + "text");
        Plain(a < b);
        Debug.Assert(a < b);
        Debug.Assert(a < b, "message");
        ArgumentNullException.ThrowIfNull(item ?? check);
        ArgumentOutOfRangeException.ThrowIfNegative(a - b);
        (a + b).Text();
        check(a + b);
        Check(a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a);
    }
}
