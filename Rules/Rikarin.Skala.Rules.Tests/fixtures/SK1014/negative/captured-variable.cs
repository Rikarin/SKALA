using System; class C { bool M(int x) { Action change = () => x++; return x > 0 && x < 10; } }
