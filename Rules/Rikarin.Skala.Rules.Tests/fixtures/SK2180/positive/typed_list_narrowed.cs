using System.Collections.Generic;

class Shape { }

sealed class Circle : Shape { }

static class Draw {
    public static void All(List<Shape> shapes) {
        // The sequence yields `Shape`; the loop asserts `Circle` about every one of them.
        foreach (Circle c in shapes) {
            Use(c);
        }
    }

    static void Use(Circle c) { }
}
