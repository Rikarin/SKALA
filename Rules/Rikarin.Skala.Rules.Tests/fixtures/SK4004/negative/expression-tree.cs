using System; using System.Linq.Expressions; class C { Expression<Func<T, T, int>> M<T>() where T : struct, IComparable<T> => (x, y) => ((IComparable<T>)x).CompareTo(y); }
