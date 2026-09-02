// ⚠ `Order` is a SQL keyword and this fusion is deliberate: the table is `OrderItems`. It is why
// the rule tests the word the *right* fragment opens with and never the one the left one ends on.
public sealed class Queries {
    public string Items() =>
        "select * from Order"
        + "Items where id = 1";
}
