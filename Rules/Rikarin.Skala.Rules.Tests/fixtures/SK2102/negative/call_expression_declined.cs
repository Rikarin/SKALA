using System.Diagnostics;

// ⚠ A call is not the accepted grammar, so the whole attribute is withdrawn — even though
// `Describe` really is absent. Declining what cannot be proved is the safe direction: an
// extension method, a method group and a member of another type all look like this.
[DebuggerDisplay("{Describe()}")]
sealed class Basket {
    public string Name => "basket";
}
