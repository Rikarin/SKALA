namespace Contoso.Design;

// The ordinary case, and the one that has to stay quiet for the rule to be worth anything.
public sealed class Pricing {
    public decimal Total(int quantity) => Discount(quantity);

    static decimal Discount(int quantity) => quantity > 10 ? 0.1m : 0m;
}
