class Base {
    public int Rank() => 1;
}

sealed class Leaf : Base {
    public int Read() {
        int Rank() => 9;

        return Rank() + base.Rank();
    }
}
