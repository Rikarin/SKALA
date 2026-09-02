using System;
using System.IO;

class C {
    bool Same(string a, string b) => Path.GetFullPath(a).Equals(b, StringComparison.OrdinalIgnoreCase);
    bool Under(string root, string candidate) =>
        candidate.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
    bool Named(string a, string b) => Path.GetFileName(a).Equals(b, StringComparison.InvariantCultureIgnoreCase);
}
