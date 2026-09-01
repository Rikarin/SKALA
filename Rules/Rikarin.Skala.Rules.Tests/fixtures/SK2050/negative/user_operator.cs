struct S { public static int operator /(S a, S b) => 1; }
class C { double M(S hits, S total) => hits / total; }
