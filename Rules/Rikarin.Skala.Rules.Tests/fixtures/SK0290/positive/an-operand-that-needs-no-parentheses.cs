public static class UnparenthesisedOperand {
    // Every whitelisted position accepts an arbitrary expression, so the fix is two deletions and
    // never has to add a bracket back: `int? value = flag ? left : right;` is what this becomes.
    public static int? Go(bool flag, int left, int right) {
        int? value = new int?(flag ? left : right);
        return value;
    }
}
