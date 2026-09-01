using System;

public sealed class Pipeline {
    public Func<string, int> Length { get; } =
        text => text is null ? throw new ArgumentNullException("text") : text.Length;

    public Action<string, int> Pair { get; } = (label, size) => {
        if (size < 0) {
            throw new ArgumentOutOfRangeException("size", size, label);
        }
    };
}
