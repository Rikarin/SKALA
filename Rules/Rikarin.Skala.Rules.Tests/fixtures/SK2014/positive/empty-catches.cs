using System;

sealed class Reader {
    public void Read() {
        try {
            Parse();
        } catch (FormatException) { }

        try {
            Parse();
        } catch { }
    }

    static void Parse() { }
}
