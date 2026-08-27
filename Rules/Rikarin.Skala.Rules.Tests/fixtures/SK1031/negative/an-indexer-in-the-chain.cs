using System.Collections.Generic;

public sealed class Order {
    public string? Label;
}

// The write goes through an indexer, so the target's chain is not a chain of names and the rule has
// no link to attach the `?` to. It is also the case the "evaluated once instead of twice" argument
// does not cover: an indexer may return a different object on the second call.
public sealed class Desk {
    public void Label(List<Order>? orders, string label) {
        if (orders is not null) {
            orders[0].Label = label;
        }
    }
}
