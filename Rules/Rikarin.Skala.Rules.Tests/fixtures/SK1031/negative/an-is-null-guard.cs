public sealed class Order;

public sealed class Customer {
    public Order? Current;
}

// `is null` proves the opposite of what the rewrite needs.
public sealed class Desk {
    public void Assign(Customer? customer, Order order) {
        if (customer is null) {
            System.Console.WriteLine("none");
        }
    }
}
