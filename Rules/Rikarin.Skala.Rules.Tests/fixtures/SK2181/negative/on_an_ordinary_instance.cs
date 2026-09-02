using System;

sealed class Widget { }

static class Describe {
    public static Type Of(Widget widget) => widget.GetType();
}
