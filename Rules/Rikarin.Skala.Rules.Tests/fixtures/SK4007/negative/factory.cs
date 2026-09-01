struct Large { public long A, B, C, D, E, F, G, H, I; } class C { void Use(Large value) { } Large Create() => new(); void M() { for (int i = 0; i < 10; i++) Use(Create()); } }
