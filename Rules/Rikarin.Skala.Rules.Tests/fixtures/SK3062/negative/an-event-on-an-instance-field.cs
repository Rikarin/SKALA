using System;

// ⚠ The single most important thing this rule stays silent about, and the reason the rule is shaped
// the way it is. `button.Click += OnClick;` in a constructor is in nearly every UI type ever
// written, and it is correct: the event's owner is a field of this object and cannot outlive it. A
// rule that reports this would be switched off wholesale and take the four real shapes with it.
public sealed class Button {
    public event EventHandler? Click;

    public void Press() => Click?.Invoke(this, EventArgs.Empty);
}

public sealed class Screen {
    readonly Button button = new();

    int presses;

    public Screen() {
        button.Click += OnClick;
    }

    public int Presses => presses;

    public void Tap() => button.Press();

    void OnClick(object? sender, EventArgs e) => presses++;
}
