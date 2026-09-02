// The operand already has the tested type. The type does not move, so the fix is the operand
// itself rather than a cast.
sealed class Widget { }

sealed class Consumer {
    public Widget? Same(Widget widget) => widget as Widget;
}
