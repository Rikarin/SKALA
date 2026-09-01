struct Slot {
    public int Index;

    public override bool Equals(object? other) => other is Slot slot && slot.Index == Index;

    public override int GetHashCode() => Index;
}
