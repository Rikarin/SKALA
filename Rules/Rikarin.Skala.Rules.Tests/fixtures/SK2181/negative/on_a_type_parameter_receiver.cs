using System;

static class Describe {
    // The receiver's type is a type parameter, which is classified against the constraint set.
    public static Type Of<T>(T value) where T : Type => value.GetType();
}
