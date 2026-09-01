using System;
using System.IO;

sealed class ParseException : Exception {
    public ParseException() { }

    public ParseException(Exception inner) : base("parsing failed", inner) { }

    public ParseException(string message, int line) : base(message) { }

    public ParseException(string message, int line, Exception inner) : base(message, inner) { }
}

sealed class Parser {
    // No arguments at all, and a constructor that takes only the cause.
    public void Empty(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new ParseException();
        }
    }

    // Two arguments, and an overload that takes the same two plus the cause.
    public void TwoArguments(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new ParseException("could not read the file", 0);
        }
    }

    // A `throw` expression, not a statement.
    public string ThrowExpression(string path) {
        try {
            return File.ReadAllText(path);
        } catch (IOException error) {
            return Fallback() ?? throw new ParseException("no fallback available", 0);
        }
    }

    static string? Fallback() => null;
}
