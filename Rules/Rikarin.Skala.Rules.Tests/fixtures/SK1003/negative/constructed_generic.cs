class C<T> { int count; public int Count { get => count; set => count = value; } int Other(C<int> other) => other.count; }
