using System; class C { int M<T>(T x, T y) where T : struct, IComparable<T> => x.CompareTo(y); }
