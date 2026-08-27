public sealed class Customer {
    public int Version;
}

// A compound assignment reads the target as well as writing it. C# 14 does allow `x?.P += 1`, but
// the read and the write are not the same operation as the plain write this rule proves, so the
// shape is left alone rather than assumed.
public sealed class Desk {
    public void Bump(Customer? customer) {
        if (customer is not null) {
            customer.Version += 1;
        }
    }
}
