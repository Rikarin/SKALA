using System;

// The modification is in the *receiver*, which is evaluated before the null test and therefore
// unconditionally.
public sealed class Slots {
    int index;

    readonly Action[] handlers = new Action[4];

    public void Fire() {
        handlers[index++]?.Invoke();
    }
}
