enum Kind { A, B, C } class C { int M(Kind x) { if (x == Kind.A) return 1; else if (x == Kind.B) return 2; else return 3; } }
