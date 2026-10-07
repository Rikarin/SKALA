using System;

public sealed class Button {
    public event EventHandler? Click;

    public void Raise() => Click?.Invoke(this, EventArgs.Empty);
}

public static class Wiring {
    public static void Attach(Button button) {
        button.Click += delegate(object? sender, EventArgs e) { Console.WriteLine("clicked"); };
    }
}
