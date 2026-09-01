namespace Contoso.Design;

// An ordinary reference return. "Not found" is a real answer for a single object in a way it is not
// for a sequence, and the nullable annotation is the language's way of saying so — this rule is about
// the contract a sequence type makes, not about null in general.
public sealed class Customer;

public sealed class Directory {
    public Customer Find(string name) {
        return null;
    }
}
