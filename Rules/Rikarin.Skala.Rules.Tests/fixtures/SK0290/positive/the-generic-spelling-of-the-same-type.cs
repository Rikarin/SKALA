using System;

public static class GenericSpelling {
    // `new Nullable<int>(x)` and `new int?(x)` are the same symbol, so both spellings are covered.
    public static int? Go(int value) {
        Nullable<int> wrapped = new Nullable<int>(value);
        return wrapped;
    }
}
