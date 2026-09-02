using System;
using System.IO;

class C {
    bool Same(FileInfo file, string other) => file.FullName.Equals(other, StringComparison.OrdinalIgnoreCase);
    bool InFolder(DirectoryInfo directory, string other) =>
        other.StartsWith(directory.FullName, StringComparison.OrdinalIgnoreCase);
    bool ByName(FileInfo file, string other) => file.Name.Equals(other, StringComparison.CurrentCultureIgnoreCase);
}
