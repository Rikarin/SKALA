namespace Contoso.Design;

// A constructor with no accessibility modifier is private, and this one is the only one there is.
public sealed class Ledger {
    Ledger() { }

    public int Balance => 0;
}
