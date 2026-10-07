interface ITable {
    int this[int row] => row;
}

interface IStrictTable : ITable {
    abstract int ITable.this[int row] { get; }
}
