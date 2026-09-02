// A name, a literal, a member-access path or a call is not a binary expression, so there is no
// grouping question to answer and nothing to parenthesise.
using System.IO;

class C {
    int Names(int flags, int mask) => flags & mask;

    int Literal(int flags) => flags & 0x0F;

    FileAttributes Enum(FileAttributes flags) => flags & FileAttributes.ReadOnly;

    int Call(int flags) => flags & Mask();

    int Unary(int flags, int mask) => flags & ~mask;

    static int Mask() => 0x0F;
}
