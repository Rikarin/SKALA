// ⚠ The guard the whole rule is built around. `System.String` comes from a referenced assembly, and
// a reference assembly does not publish private members — so "no member named `_firstChar`" is what
// Roslyn answers whether the field exists or not. At `error` severity that would break every correct
// cross-assembly accessor, which is most of the attribute's real use. The rule declines and says so.
using System.Runtime.CompilerServices;

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_firstChar")]
    public static extern ref char FirstChar(string text);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "definitely_not_a_real_field_on_string")]
    public static extern ref char NotThere(string text);
}
