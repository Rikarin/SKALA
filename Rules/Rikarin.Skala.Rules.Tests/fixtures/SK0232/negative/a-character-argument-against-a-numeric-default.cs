public static class Markers {
    static int Emit(int code = 65) => code;

    // ⚠ `'A'` and `65` are the same number and not the same sentence. The numeric widening this
    // rule now understands deliberately stops short of `char`: deleting the argument would delete
    // which of the two spellings the author meant.
    public static int Start() => Emit('A');
}
