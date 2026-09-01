struct Number { public static double operator +(Number a, Number b) => 0.1; } class C { bool M(Number x, Number y, double expected) => x + y == expected; }
