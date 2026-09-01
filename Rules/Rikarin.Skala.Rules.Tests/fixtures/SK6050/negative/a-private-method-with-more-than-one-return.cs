namespace Contoso.Design;

// Two returns is a branch, and a branch reads something — if not a parameter then state. Either way
// it is not the placeholder this rule is about, so "every return is the same constant" is
// deliberately not the predicate.
public sealed class Gate {
    bool open;

    public bool Allow(string caller) => Check(caller);

    bool Check(string caller) {
        if (open) {
            return true;
        }

        return false;
    }
}
