using System;

namespace Contoso.Design;

// Beside a private constructor, this is what reflection looks like from here.
public sealed class Widget {
    private Widget() { }

    public int Size => 0;
}

public static class Widgets {
    public static Type Kind => typeof(Widget);
}
