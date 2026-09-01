using System;

public static class Importer {
    public sealed class MalformedHeaderException : Exception {
        public MalformedHeaderException(string message) : base(message) { }
    }
}
