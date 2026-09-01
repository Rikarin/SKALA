class Bag { public int Length => 1; public int this[int i] => 1; } class C { bool M(Bag? a) => a != null && a.Length == 1 && a[0] == 1; }
