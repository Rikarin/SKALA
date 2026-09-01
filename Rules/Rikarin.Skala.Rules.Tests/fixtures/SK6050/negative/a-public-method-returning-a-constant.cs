namespace Contoso.Design;

// The rule can see every caller of a private member and none of a public one, so "the arguments are
// computed and thrown away" is not a statement it can make here. That recall cost is the whole
// design of the rule and is not a gap to be widened later.
public sealed class Pricing {
    public decimal Discount(Customer customer, int quantity) => 0m;

    internal decimal Surcharge(Customer customer) => 0m;
}

public sealed class Customer {
    public bool IsPreferred { get; init; }
}
