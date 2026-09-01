interface I { int Value { get; } } class C { int M<T>(T value) where T : struct, I => ((I)value).Value; }
