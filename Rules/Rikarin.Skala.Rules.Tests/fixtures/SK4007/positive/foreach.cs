struct Large { public long A, B, C, D, E, F, G, H, I; } class C { void Use(Large value) { } void M(Large[] values) { foreach (var value in values) Use(value); } }
