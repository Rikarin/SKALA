using System;

public sealed class Buffers {
    // `Array.Copy` with one array is how a shift is written, so it is not in the table even though
    // `Array.CopyTo` with one array is.
    public static void Shift(int[] buffer, int used) {
        Array.Copy(buffer, 1, buffer, 0, used - 1);
    }
}
