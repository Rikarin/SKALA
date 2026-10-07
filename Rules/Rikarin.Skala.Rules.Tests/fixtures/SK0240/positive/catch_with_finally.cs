using System;

// The try block calls nothing, so no `finally` can sit beneath the `catch` — the one the statement
// carries itself runs after the `catch` either way.
class C {
    static readonly int[] Values = new int[4];
    static int _closed;

    public static int Read(int index) {
        var value = 0;
        try {
            value = Values[index];
        } catch (IndexOutOfRangeException) {
            throw;
        } finally {
            _closed++;
        }

        return value;
    }

    public static int Closed => _closed;
}
