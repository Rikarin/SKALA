struct S { public static int operator *(S a, int b) => 0; }
class C { int M(S weight) => weight * 1; }
