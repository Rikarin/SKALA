using System;

public static class Pairs {
    public static int Sum(int left, int right) {
        Func<int, int, int> add = (x, y) => x + y;
        return add(left, right);
    }
}
