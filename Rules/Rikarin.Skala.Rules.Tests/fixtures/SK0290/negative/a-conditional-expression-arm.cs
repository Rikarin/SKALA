public static class ConditionalArm {
    // ⚠ `flag ? 5 : null` has no common type — `CS0173` — so the wrapper is what gives the
    // conditional its type and the deletion would not compile.
    public static int? Go(bool flag) => flag ? new int?(5) : null;
}
