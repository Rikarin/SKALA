// ⚠ Hosted, not missed. A non-nullable reference type in a file where nullable warnings are on is
// exactly CS8618's territory — verified on a probe, with an explicit constructor and an implicit
// one — and ADR-008 makes hosting the platform's own diagnostic the right outcome. Two findings on
// one declaration would be the same defect counted twice.
sealed class Account {
    public string Owner { get; }

    public int Length => Owner.Length;
}
