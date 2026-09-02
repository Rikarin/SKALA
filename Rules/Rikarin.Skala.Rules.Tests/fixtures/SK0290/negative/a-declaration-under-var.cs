public static class UnderVar {
    // ⚠ The trap the whole whitelist exists for. `var value = new int?(5);` types `value` as `int?`
    // and `var value = 5;` types it as `int`, and `GetTypeInfo` on the `var` keyword answers `int?`
    // for both — only the syntax separates them.
    public static bool Go() {
        var value = new int?(5);
        return value.HasValue;
    }
}
