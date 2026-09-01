class C { int Value { get; set; } C Next() => new C(); bool M() => Next().Value == Next().Value; }
