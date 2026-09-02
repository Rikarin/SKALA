// ⚠ The look-alike that isolates the second of the two conditions, and it needed its own fixture:
// `inner_behind_value.cs` declines earlier, at "the property has a field of its own", so nothing
// was holding the `OwnerOf` test on the hook. Here `Amount` does have its own `amount`, and the
// getter deliberately returns `rounded` instead — which backs no property at all, so the names were
// chosen rather than crossed and there is nothing to report.
sealed class Money {
    decimal amount;
    decimal rounded;

    public decimal Amount {
        get => rounded;

        set {
            amount = value;
            rounded = decimal.Round(value, 2);
        }
    }

    public decimal Raw => amount;
}
