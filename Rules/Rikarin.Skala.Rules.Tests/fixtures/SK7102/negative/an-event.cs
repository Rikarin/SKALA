using System;

public sealed class Button {
    /// <summary>Raised on a click.</summary>
    /// <returns>Nothing a handler returns is read.</returns>
    public event EventHandler? Clicked;

    /// <summary>Raises <see cref="Clicked" />.</summary>
    public void Click() => Clicked?.Invoke(this, EventArgs.Empty);
}
