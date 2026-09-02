using System.IO;

namespace Corpus.Safe;

/// <summary>
///     SK5042's twin: the same calls with the world-writable bit removed the way a reviewer would
///     remove it — owner-only, group-shared, world-*readable*, and the sticky-bit drop box that is a
///     deliberate design rather than an accident.
/// </summary>
public static class FilePermissions {
    public static void OwnerOnly(string path) =>
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

    public static void GroupShared(string path) =>
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
        );

    /// <summary>
    ///     ⚠ The file that carries the rule's narrowing. Plain <c>File.WriteAllText</c> already creates
    ///     at <c>0644</c> because that is what the process umask says, so a rule reporting
    ///     <c>OtherRead</c> would report every file-writing call in existence.
    /// </summary>
    public static void WorldReadable(string path) =>
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead
        );

    /// <summary>⚠ Mode 1777 — what <c>/tmp</c> itself is, and the rule's one escape.</summary>
    public static void DropBox(string path) =>
        Directory.CreateDirectory(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
            | UnixFileMode.StickyBit
        );

    public static void FromAVariable(string path, UnixFileMode mode) => File.SetUnixFileMode(path, mode);

    /// <summary>
    ///     ⚠ The refuted half of #145, kept here so the refutation is asserted rather than only
    ///     written down: <c>CreateTempSubdirectory</c> creates at <c>0700</c> and
    ///     <c>GetTempFileName</c> at <c>0600</c> through <c>mkstemp</c>.
    /// </summary>
    public static string Scratch() => Directory.CreateTempSubdirectory("corpus").FullName;

    public static string Temporary() => Path.GetTempFileName();
}
