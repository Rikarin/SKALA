public sealed class Order;

public sealed class Customer {
    public Order? Current;
}

// The receiver is a call. The guard evaluates it once and the assignment evaluates it again, so a
// rewrite to one evaluation is a change the rule cannot prove is free.
public sealed class Desk {
    Customer? Find(int id) => id > 0 ? new Customer() : null;

    public void Assign(int id, Order order) {
        if (Find(id) is not null) {
            Find(id)!.Current = order;
        }
    }
}
