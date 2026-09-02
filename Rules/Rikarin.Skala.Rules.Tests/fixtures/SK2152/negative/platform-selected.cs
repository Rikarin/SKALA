// The shape the rule asks for, and therefore the shape it must never report: the comparison is
// chosen at run time, exactly as `SarifWriter.PathComparison` does it in Skala's own tree. The
// argument is not a constant, so the finding's precondition is absent.
using System;
using System.IO;

class C {
    static StringComparison PathComparison { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    readonly StringComparison configured = StringComparison.OrdinalIgnoreCase;

    bool Same(string a, string b) => Path.GetFullPath(a).Equals(b, PathComparison);
    bool Under(FileInfo file, string other) => file.FullName.StartsWith(other, PathComparison);
    bool Configured(string a, string b) => Path.GetFullPath(a).Equals(b, configured);
    bool Passed(string a, string b, StringComparison how) => Path.GetFullPath(a).Equals(b, how);
}
