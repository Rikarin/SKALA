namespace Contoso.Design;

// `Circle` is sealed and `Square` is its sibling, not its subclass. A leaf of a hierarchy asking about
// another leaf is a question about the hierarchy that the leaf does not own — wrong, perhaps, but not
// this rule's inversion: nothing is edited in `Circle` when a new `Shape` appears.
public class Shape;

public sealed class Circle : Shape {
    public bool IsSquare() => this is Square;
}

public sealed class Square : Shape;
