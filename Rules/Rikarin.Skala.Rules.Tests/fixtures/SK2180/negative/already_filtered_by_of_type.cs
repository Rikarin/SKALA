using System.Collections.Generic;
using System.Linq;

class Shape { }

sealed class Circle : Shape { }

static class Draw {
    public static void All(List<Shape> shapes) {
        foreach (var c in shapes.OfType<Circle>()) {
            Use(c);
        }
    }

    static void Use(Circle c) { }
}
