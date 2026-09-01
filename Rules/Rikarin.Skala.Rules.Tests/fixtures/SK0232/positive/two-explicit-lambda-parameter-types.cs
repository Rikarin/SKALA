using System;

public static class Adding {
    public static int Sum(int left, int right) {
        Func<int, int, int> add = (int a, int b) => a + b;
        return add(left, right);
    }
}
