public sealed class AssignedField {
    int? total;

    public void Set(int value) {
        total = new int?(value);
    }

    public bool HasTotal() => total.HasValue;
}
