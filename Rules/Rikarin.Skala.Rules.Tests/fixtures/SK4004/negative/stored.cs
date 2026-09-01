using System; class C { IComparable<T> M<T>(T value) where T : struct, IComparable<T> => (IComparable<T>)value; }
