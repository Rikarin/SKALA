using System.Collections.Generic;

class Shape { }

static class Draw {
    public static void All(List<Shape> shapes) {
        foreach (Shape shape in shapes) {
            Use(shape);
        }
    }

    static void Use(Shape s) { }
}
