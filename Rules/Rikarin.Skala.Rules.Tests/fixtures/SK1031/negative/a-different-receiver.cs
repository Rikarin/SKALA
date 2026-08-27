public sealed class Order;

public sealed class Customer {
    public Order? Current;
}

public sealed class Desk {
    public void Assign(Customer? customer, Customer other, Order order) {
        if (customer is not null) {
            other.Current = order;
        }
    }
}
