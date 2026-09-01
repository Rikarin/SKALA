struct Large { public long A, B, C, D, E, F, G, H, I; } class C { void Use(ref Large value) { } void M(Large value) { for (int i = 0; i < 10; i++) Use(ref value); } }
