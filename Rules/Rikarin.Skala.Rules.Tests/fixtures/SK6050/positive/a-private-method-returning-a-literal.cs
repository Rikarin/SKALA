namespace Contoso.Design;

public sealed class Pricing {
    public decimal Total(Customer customer, int quantity) => quantity * (1m - Discount(customer, quantity));

    decimal Discount(Customer customer, int quantity) => 0m;
}

public sealed class Customer {
    public bool IsPreferred { get; init; }
}
