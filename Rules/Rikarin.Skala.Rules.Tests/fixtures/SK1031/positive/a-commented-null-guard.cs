// ⚠ #302's shape (#325). The guard asked over the `if` statement's FULL span, so a comment above
// the guard — the natural place to say why it is there — silenced the rule. ⚠ The fix replaces
// `statement.Span`, which starts at the `if` keyword, and appends its own `;`: it never rewrites
// the line above, so there was nothing for the wider question to protect.
public sealed class Order;

public sealed class Customer {
    public Order? Current;
}

public sealed class Desk {
    public void Assign(Customer? customer, Order order) {
        // only assign once we know there is a customer to assign to
        if (customer != null)
            customer.Current = order;
    }
}
