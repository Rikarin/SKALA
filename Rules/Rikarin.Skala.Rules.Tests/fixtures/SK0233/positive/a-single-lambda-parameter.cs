using System;

public static class Doubling {
    public static int Twice(int value) {
        Func<int, int> twice = (n) => n * 2;
        return twice(value);
    }
}
