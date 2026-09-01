using System;

public static class Comparable {
    public static int Order<T>(T left, T right) where T : IComparable<T> => left.CompareTo(right);
}
