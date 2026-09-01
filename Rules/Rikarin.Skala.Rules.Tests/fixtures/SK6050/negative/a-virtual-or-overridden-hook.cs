namespace Contoso.Design;

// A `virtual` method whose body is a constant is a default a derived type is invited to replace,
// which is the point of the modifier rather than an unfinished body. `override` is the same claim
// from the other side. Both are excluded regardless of accessibility.
public class Policy {
    protected virtual int Retries(string operation) => 3;
}

public sealed class Aggressive : Policy {
    protected override int Retries(string operation) => 10;
}
