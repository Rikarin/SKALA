using System.Runtime.CompilerServices;

public static class Trace {
    // A handler nobody in the framework wrote. The target type carries
    // `[InterpolatedStringHandler]`, which is the only thing that can be detected — never the
    // method's name.
    public static void Write(double bleed) =>
        Emit($"the bleed is {bleed:F1} kg/s " + $"and the handler is ours");

    static void Emit(ref TraceHandler handler) => _ = handler.Length;
}

[InterpolatedStringHandler]
public ref struct TraceHandler {
    public TraceHandler(int literalLength, int formattedCount) => Length = literalLength + formattedCount;

    public int Length { get; private set; }

    public void AppendLiteral(string value) => Length += value.Length;

    public void AppendFormatted<T>(T value, int alignment = 0, string? format = null) =>
        Length += alignment + (format?.Length ?? 0) + (value is null ? 0 : 1);
}
