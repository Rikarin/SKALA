public static class LocalFunctionReturn {
    public static int? Go(int value) {
        int? Wrap() {
            return new int?(value);
        }

        return Wrap();
    }
}
