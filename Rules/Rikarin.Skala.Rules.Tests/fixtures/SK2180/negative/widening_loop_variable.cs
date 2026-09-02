using System.Collections.Generic;

class Shape { }

sealed class Circle : Shape { }

static class Draw {
    // The loop widens rather than narrows, which is the conversion an assignment would make anyway.
    public static void All(List<Circle> circles) {
        foreach (Shape shape in circles) {
            Use(shape);
        }
    }

    static void Use(Shape s) { }
}
