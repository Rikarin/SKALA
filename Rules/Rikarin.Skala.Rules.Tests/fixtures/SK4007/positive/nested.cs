struct Part { public long A, B, C; } struct Large { public Part A, B, C; } class C { void Use(Large value) { } void M(Large value, int n) { while (n-- > 0) Use(value); } }
