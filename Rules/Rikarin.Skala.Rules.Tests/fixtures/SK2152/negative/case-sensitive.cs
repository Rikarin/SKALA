// The other direction, deliberately not reported. A hard-coded `Ordinal` on a path is wrong on
// Windows for the same reason `OrdinalIgnoreCase` is wrong on Linux, but reporting it would fire on
// every correct comparison written by someone targeting Linux only, and nothing in the source
// separates the two.
using System;
using System.IO;

class C {
    bool Same(string a, string b) => Path.GetFullPath(a).Equals(b, StringComparison.Ordinal);
    bool ByName(FileInfo file, string other) => file.FullName.Equals(other, StringComparison.Ordinal);
    bool Cultured(string a, string b) => Path.GetFileName(a).Equals(b, StringComparison.InvariantCulture);
}
