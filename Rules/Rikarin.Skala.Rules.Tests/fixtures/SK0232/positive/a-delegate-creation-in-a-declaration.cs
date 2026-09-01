using System;

public static class Handlers {
    static void OnChanged(object? sender, EventArgs e) { }

    public static EventHandler Create() {
        EventHandler handler = new EventHandler(OnChanged);
        return handler;
    }
}
