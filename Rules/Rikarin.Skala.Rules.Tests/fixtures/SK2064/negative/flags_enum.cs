// ⚠ The catastrophe this rule must never cause. `&` on a [Flags] enum is the only way to write
// the operation and has no `&&` form at all.
using System.IO;

class C {
    bool IsReadOnly(FileAttributes attributes) => (attributes & FileAttributes.ReadOnly) != 0;

    FileAttributes Combine(FileAttributes a, FileAttributes b) => a | b;
}
