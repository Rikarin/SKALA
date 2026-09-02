using System.IO;
using Microsoft.Win32.SafeHandles;

namespace Corpus.Vulnerable;

/// <summary>SK5042 — a file or directory created writable by every local user.</summary>
/// <remarks>
///     Every mode here carries <c>OtherWrite</c> without the sticky bit, so any account on the machine
///     may replace the contents of what the call creates — and this program reads it back.
/// </remarks>
public static class FilePermissions {
    const UnixFileMode Shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite;

    public static void ByPath(string path) =>
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite
        );

    public static void ByHandle(SafeFileHandle handle) =>
        File.SetUnixFileMode(
            handle,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite
        );

    public static void Directory(string path) =>
        System.IO.Directory.CreateDirectory(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
        );

    public static FileStream Stream(string path) =>
        new FileStream(
            path,
            new FileStreamOptions {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite
            }
        );

    public static void ByConstant(string path) => File.SetUnixFileMode(path, Shared);
}
