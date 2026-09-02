using System.Collections.Generic;

class Shape { }

static class Generic {
    // The loop variable is a type parameter, so the conversion is classified against the constraint
    // set rather than against the type the method is instantiated with — `SK2121`'s exclusion too.
    public static void All<T>(List<Shape> shapes) where T : Shape {
        foreach (T item in shapes) {
            Use(item);
        }
    }

    static void Use(Shape s) { }
}
