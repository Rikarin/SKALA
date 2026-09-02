using System.Collections.Generic;
using System.Linq;

public sealed class Widget;

public sealed class Registry {
    public static IEnumerable<Widget> Widgets(IEnumerable<object> values) =>
        values.Where((value) => value is Widget).Cast<Widget>();
}
