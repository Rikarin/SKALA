// An ordinary interface indexer is abstract whether or not the keyword is written.
interface ITable {
    abstract int this[int row] { get; }
}
