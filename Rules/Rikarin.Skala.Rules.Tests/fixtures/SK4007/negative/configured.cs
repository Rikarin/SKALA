// analyzer-option: dotnet_code_quality.SK4007.threshold = 128
struct Large { public long A, B, C, D, E, F, G, H, I; } class C { void Use(Large value) { } void M(Large value) { for (int i = 0; i < 10; i++) Use(value); } }
