// A record's copy constructor does not run field initializers, so the evidence has a different
// shape from the one the walk reads. The whole type is declined.
public record Shipment {
    readonly int weight = 1;

    public Shipment(int given) {
        weight = given;
    }

    public int Weight => weight;
}
