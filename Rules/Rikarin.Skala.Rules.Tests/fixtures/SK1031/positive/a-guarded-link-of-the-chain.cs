public sealed class Order {
    public string? Label;
}

public sealed class Customer {
    public Order? Current;
}

public sealed class Desk {
    public void Label(Customer customer, string label) {
        if (customer.Current is not null) {
            customer.Current.Label = label;
        }
    }
}
