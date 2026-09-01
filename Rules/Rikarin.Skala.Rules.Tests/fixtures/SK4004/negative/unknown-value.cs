using System; class C { int M<T>(T x, T y) where T : IComparable<T> => ((IComparable<T>)x).CompareTo(y); }
