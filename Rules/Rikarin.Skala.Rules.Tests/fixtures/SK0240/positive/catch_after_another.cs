using System;

class C {
    static readonly int[] Values = new int[4];
    static Exception? _last;

    public static int Read(int index) {
        var value = 0;
        try {
            value = Values[index];
        } catch (IndexOutOfRangeException error) {
            _last = error;
        } catch (InvalidOperationException) {
            throw;
        }

        return value;
    }

    public static Exception? Last => _last;
}
