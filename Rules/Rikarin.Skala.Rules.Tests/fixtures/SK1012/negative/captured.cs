using System; class C { int M(int x) { Action a = () => x++; if (x == 0) return 1; else if (x == 1) return 2; else return 3; } }
