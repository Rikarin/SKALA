using System;

public static class Logging {
    public static EventHandler Attach() {
        EventHandler handler = delegate(object? sender, EventArgs e) { Console.WriteLine(sender); };
        return handler;
    }
}
