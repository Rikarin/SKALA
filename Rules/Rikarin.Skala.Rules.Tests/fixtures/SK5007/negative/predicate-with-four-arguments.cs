using System;

public static class Filters {
    public static Func<int, int, int, int, bool> Always() => (_, _, _, _) => true;
}
