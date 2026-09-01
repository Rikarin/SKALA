class Lazy<T> { public Lazy(bool safe) { } } class C { static Lazy<int> Value = new(false); }
