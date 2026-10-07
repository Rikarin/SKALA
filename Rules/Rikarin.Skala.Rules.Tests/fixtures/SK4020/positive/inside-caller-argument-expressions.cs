// #422: every lambda below captures nothing, so SK4020 reports it — and every one but the last three
// sits inside text a [CallerArgumentExpression] parameter captures, so writing `static ` there changes
// the string Probe.Run() returns. The fix stays offered; it is no longer safe at these call sites.
using System;
using System.Runtime.CompilerServices;

public delegate string Checker(Func<int, int> f, [CallerArgumentExpression("f")] string text = "");

[InterpolatedStringHandler]
public ref struct Message {
    readonly System.Text.StringBuilder builder;

    public Message(int literalLength, int formattedCount) => builder = new();

    public Message(int literalLength, int formattedCount, bool condition, out bool append) {
        builder = new();
        append = !condition;
    }

    public void AppendLiteral(string value) => builder.Append(value);

    public void AppendFormatted<T>(T value) => builder.Append(value);

    public override string ToString() => builder.ToString();
}

public sealed class Box {
    public Box(Func<int, int> f, [CallerArgumentExpression("f")] string text = "") => Text = text;

    public string Text { get; }

    public string this[Func<int, int> f, [CallerArgumentExpression("f")] string text = ""] => text;
}

public static class Captures {
    public static int Apply(Func<int, int> f) => f(1);

    public static string Capture(object? value, [CallerArgumentExpression("value")] string text = "") => text;

    public static string Generic<T>(T value, [CallerArgumentExpression("value")] string text = "") => text;

    public static string Pick(int value, [CallerArgumentExpression("value")] string text = "") => "int " + text;

    public static string Pick(string value, [CallerArgumentExpression("value")] string text = "") => "string " + text;

    public static string Many([CallerArgumentExpression("values")] string text = "", params Func<int, int>[] values) =>
        text;

    public static string Text<T>(this T value, [CallerArgumentExpression("value")] string text = "") => text;

    public static string Log(Message message, [CallerArgumentExpression("message")] string text = "") => text;

    public static string Assert(
        bool condition,
        [InterpolatedStringHandlerArgument("condition")] ref Message message,
        [CallerArgumentExpression("condition")] string text = ""
    ) => text;

#pragma warning disable CS8963 // The name is wrong on purpose: nothing is captured, so the fix stays safe.
    public static string Misnamed(Func<int, int> f, [CallerArgumentExpression("nothing")] string text = "none") => text;
#pragma warning restore CS8963
}

public static class Probe {
    public static string Run() {
        static string Local(Func<int, int> f, [CallerArgumentExpression("f")] string text = "") => text;

        Checker check = static (f, text) => text;
        string thrown;
        try {
            ArgumentException.ThrowIfNullOrEmpty(Captures.Apply(x => x + 1) > 0 ? "" : "set");
            thrown = "no throw";
        } catch (ArgumentException e) {
            thrown = e.ParamName ?? "";
        }

        return string.Join(
            "|",
            Captures.Capture(Captures.Apply(x => x + 1)),
            Captures.Capture(value: Captures.Apply(x => x + 2)),
            Captures.Generic<Func<int, int>>(x => x + 3),
            Captures.Pick(Captures.Apply(x => x + 4)),
            Captures.Many(values: x => x + 5),
            Captures.Apply(x => x + 6).Text(),
            Captures.Log($"{Captures.Apply(x => x + 7)}"),
            Captures.Assert(Captures.Apply(x => x + 8) > 0, $"hole {Captures.Apply(x => x + 9)}"),
            new Box(x => x + 10).Text,
            new Box(static x => x)[x => x + 11],
            check(x => x + 12),
            Local(x => x + 13),
            thrown,
            Captures.Capture(text: "explicit", value: Captures.Apply(x => x + 14)),
            Captures.Misnamed(x => x + 15),
            Captures.Apply(x => x + 16).ToString()
        );
    }
}
