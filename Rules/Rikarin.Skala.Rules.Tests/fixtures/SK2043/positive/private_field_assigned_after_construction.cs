sealed class Cell {
    int slot;

    public Cell(int slot) => this.slot = slot;

    public void Move(int value) => slot = value;

    public override bool Equals(object? other) => other is Cell cell && cell.slot == slot;

    public override int GetHashCode() => slot;
}
