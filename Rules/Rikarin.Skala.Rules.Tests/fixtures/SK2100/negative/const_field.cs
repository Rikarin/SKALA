using System;

// ⚠ A `const` is implicitly static and is *required* to carry an initializer, so neither half of
// the rule can be true of it — and deleting the initializer would not compile.
static class Limits {
    [ThreadStatic] const int Max = 32;

    public static int Value => Max;
}
