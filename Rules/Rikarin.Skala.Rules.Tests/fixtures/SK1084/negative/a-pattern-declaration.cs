using System.Collections.Generic;
using System.Linq;

public sealed class Widget {
    public void Draw() { }
}

public sealed class Registry {
    // The declaration would move out of scope and the body still reads it.
    public static void Render(IEnumerable<object> values) {
        foreach (var value in values) {
            if (value is Widget widget) {
                widget.Draw();
            }
        }
    }
}
