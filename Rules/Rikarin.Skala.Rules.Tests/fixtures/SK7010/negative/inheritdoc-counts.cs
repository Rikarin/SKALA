// ⚠ "treats an `<inheritdoc/>` as documentation" — rules.json, SK7010. It is a deliberate statement
// that the base member's prose applies here, which is exactly what the metric asks for.

/// <summary>Something that can be measured.</summary>
public interface IMeasurable {
    /// <summary>The measured size.</summary>
    int Size { get; }

    /// <summary>Measures again.</summary>
    void Measure();
}

/// <summary>A measurable thing.</summary>
public sealed class Measurable : IMeasurable {
    /// <inheritdoc />
    public int Size { get; private set; }

    /// <inheritdoc />
    public void Measure() => Size = 1;
}
