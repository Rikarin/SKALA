using System.Runtime.CompilerServices;

namespace Contoso.Design;

// An attribute is a reason the body is what it is that the rule cannot read: a generator's marker, a
// serializer's hook, an interop shim. Any attribute at all withdraws the finding.
public sealed class Fast {
    public int Size(string value) => Measure(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Measure(string value) => 0;
}
