using System; class C { bool M(int[]? a) { Action reset = () => a = null; return a != null && a.Length == 1 && a[0] == 1; } }
