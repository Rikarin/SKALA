using System;

public static class Typed {
    public static int Twice(int value) {
        // The typed form belongs to SK0232, which produces this shape's output.
        Func<int, int> twice = (int n) => n * 2;
        return twice(value);
    }
}
