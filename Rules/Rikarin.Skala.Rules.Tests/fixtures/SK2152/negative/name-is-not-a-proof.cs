// ⚠ The false-positive class that decides this rule. Every one of these is spelled like a path and
// none of them is provably one: the value never came out of `System.IO.Path` or off a
// `FileSystemInfo`. Name-based path detection is how this kind of rule acquires its false positives,
// so a parameter called `filePath` buys nothing here.
using System;

class C {
    bool ByName(string filePath, string other) => filePath.Equals(other, StringComparison.OrdinalIgnoreCase);
    bool Directory(string directoryName, string other) =>
        directoryName.StartsWith(other, StringComparison.OrdinalIgnoreCase);
    bool Header(string headerName) => headerName.Equals("Accept", StringComparison.OrdinalIgnoreCase);
    bool Scheme(string url) => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    bool Extension(string fullPath) => fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
}
