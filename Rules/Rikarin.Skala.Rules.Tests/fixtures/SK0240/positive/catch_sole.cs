using System;

class C {
    static readonly int[] Values = new int[4];

    public static int Read(int index) {
        var value = 0;
        try {
            value = Values[index];
        } catch (IndexOutOfRangeException) {
            throw;
        }

        return value;
    }
}

public static class Probe {
    public static string Run() => C.Read(2) + " " + C.Read(-1);
}
