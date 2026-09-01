static class LocalFunctionFixture {
    public static int Apply(int value) {
        int Double(int input) => input * 2;
        return Double(value);
    }
}
