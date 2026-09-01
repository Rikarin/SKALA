struct Large { public decimal A, B, C, D, E; } class C { void Use(Large value) { } void M(Large value, int n) { do { Use(value); } while (n-- > 0); } }
