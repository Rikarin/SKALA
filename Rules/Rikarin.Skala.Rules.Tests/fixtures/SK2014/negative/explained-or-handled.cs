using System;

sealed class Reader {
    public void Read(bool optional) {
        try {
            Parse();
        } catch (FormatException) {
            // The optional record may be malformed and is deliberately ignored.
        }

        try {
            Parse();
        } catch (FormatException) when (optional) { }

        try {
            Parse();
        } catch (FormatException exception) {
            Console.WriteLine(exception);
        }
    }

    static void Parse() { }
}
