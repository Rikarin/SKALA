public static class CompoundAssignment {
    // Only a simple assignment is whitelisted. The right side of a compound assignment is an operand
    // of a lifted operator, not a value converted to the left side's type.
    public static int? Go(int? total, int value) {
        total += new int?(value);
        return total;
    }
}
