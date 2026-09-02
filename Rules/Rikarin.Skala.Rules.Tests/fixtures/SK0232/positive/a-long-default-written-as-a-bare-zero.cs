public static class Budget {
    // ⚠ The default is a `long` and the argument is an `int` literal. Comparing the two boxed
    // constants with `Equals` answers false on the type before it reaches the value, so this shape
    // was silently missed rather than wrongly reported (#298).
    static long Allow(string name, long retries = 0) => name.Length + retries;

    public static long Start(string name) => Allow(name, 0);
}
