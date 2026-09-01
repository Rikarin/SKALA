// A soft hyphen is a legal C# identifier character (Unicode Cf). It is a naming question,
// not a value that silently differs from what it reads as, and IDE1006 is nearer to it.
// contains: U+00AD
namespace Fixtures;

sealed class Keys {
    public const string Ten­ant = "tenant-id";
}
