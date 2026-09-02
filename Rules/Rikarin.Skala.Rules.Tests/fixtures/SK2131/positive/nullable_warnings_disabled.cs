#nullable disable

// ⚠ A non-nullable reference type would be CS8618 — but only where nullable warnings are on. With
// them off the compiler says nothing, which is exactly the configuration in which this rule is the
// only thing that will speak.
sealed class Account {
    public string Owner { get; }

    public int Length => Owner.Length;
}
