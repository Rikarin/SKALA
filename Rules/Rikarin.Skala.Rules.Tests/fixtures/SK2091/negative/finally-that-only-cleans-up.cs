using System;
using System.IO;

sealed class Ordinary {
    // ⚠ The reason the rule reads the keyword and nothing else. Every one of these calls can throw,
    // and reporting them would report every `finally` ever written.
    public void Cleanup(Stream stream) {
        try {
            stream.Read(new byte[16]);
        } finally {
            stream.Flush();
            stream.Dispose();
        }
    }

    public void LogsInstead() {
        try {
            Work();
        } finally {
            Console.WriteLine("the work did not commit");
        }
    }

    // A `throw` in the `try` block, in a `catch` clause, or after the whole statement is not this.
    public void ThrowsElsewhere() {
        try {
            throw new InvalidOperationException("in the try");
        } catch (InvalidOperationException) {
            throw new NotSupportedException("in the catch");
        } finally {
            Work();
        }
    }

    static void Work() { }
}
