class C { int count; static void Set(ref int x, int value) { x = value; } public int Count { get => count; set => Set(ref count, value); } }
