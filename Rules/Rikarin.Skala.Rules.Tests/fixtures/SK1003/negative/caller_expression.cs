class C { int count; static int Read(int x, [System.Runtime.CompilerServices.CallerArgumentExpression("x")] string? text = null) => x; public int Count { get => Read(count); set => count = value; } }
