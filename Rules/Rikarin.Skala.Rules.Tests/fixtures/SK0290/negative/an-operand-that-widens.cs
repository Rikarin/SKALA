public static class WideningOperand {
    // The constructor call is converting `byte` to `int` as well as `int` to `int?`. Only an operand
    // whose type is exactly the underlying type is covered, so this shape is declined rather than
    // reasoned about.
    public static int? Go(byte value) {
        int? wrapped = new int?(value);
        return wrapped;
    }
}
