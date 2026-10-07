using System;

/// <summary>Something that changes.</summary>
public interface IChanging {
    /// <summary>Raised on a change.</summary>
    event EventHandler? Changed;
}

/// <summary>A model.</summary>
public sealed class Model : IChanging {
    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises the event.</summary>
    public void Touch() => Changed?.Invoke(this, EventArgs.Empty);
}
