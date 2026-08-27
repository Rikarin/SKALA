public sealed class Order;

public sealed class Customer {
    public Order? Current;
}

public sealed class Desk {
    public void Assign(Customer? customer, Order order) {
        if (customer is not null) {
            customer.Current = order;
        } else {
            System.Console.WriteLine("no customer");
        }
    }
}
