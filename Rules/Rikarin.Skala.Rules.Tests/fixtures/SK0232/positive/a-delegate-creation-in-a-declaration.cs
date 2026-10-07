using System;

public sealed class Handlers {
    int _seen;

    void OnChanged(object? sender, EventArgs e) => _seen++;

    // An instance method group binds `this` and is never cached, so each conversion is a new delegate,
    // exactly as each `new EventHandler(...)` was.
    public EventHandler Create() {
        EventHandler handler = new EventHandler(OnChanged);
        return handler;
    }

    public int Seen => _seen;
}

public static class Probe {
    public static string Run() {
        var handlers = new Handlers();
        var first = handlers.Create();
        var second = handlers.Create();
        first(null, EventArgs.Empty);
        return ReferenceEquals(first, second) + " " + first.Equals(second) + " " + handlers.Seen;
    }
}
