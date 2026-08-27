// `!=` here is whatever the type says it is, and `?.` tests for null. Rewriting one into the other
// is a behaviour change dressed as a style fix.
public sealed class Order;

public sealed class Customer {
    public Order? Current;

    public static bool operator ==(Customer? left, Customer? right) => ReferenceEquals(left, right);

    public static bool operator !=(Customer? left, Customer? right) => !(left == right);

    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => 0;
}

public sealed class Desk {
    public void Assign(Customer? customer, Order order) {
        if (customer != null) {
            customer.Current = order;
        }
    }
}
