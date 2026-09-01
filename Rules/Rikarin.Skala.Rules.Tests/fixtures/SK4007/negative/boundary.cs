struct Size64 { public long A, B, C, D, E, F, G, H; } class C { void Use(Size64 value) { } void M(Size64 value) { for (int i = 0; i < 10; i++) Use(value); } }
