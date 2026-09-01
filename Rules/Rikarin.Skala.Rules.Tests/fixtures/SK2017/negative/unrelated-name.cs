using System;
using System.IO;

public sealed class Forwarding {
    public void Open(string path) => Read(File.OpenRead(path));

    // ⚠ A private helper that reports its caller's parameter name is a real BCL pattern, and the
    // name it reports is the one its caller's caller can act on. Nothing in scope resembles
    // `"path"`, so the rule is silent rather than rewriting the message to `nameof(stream)`.
    static void Read(Stream stream) {
        if (stream.Length == 0) {
            throw new ArgumentException("names an empty file", "path");
        }
    }
}
