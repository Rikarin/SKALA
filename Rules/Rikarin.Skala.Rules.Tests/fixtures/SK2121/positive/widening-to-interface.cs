// The class implements the interface, so the conversion is implicit and certain.
interface IShape {
    int Sides { get; }
}

sealed class Square : IShape {
    public int Sides => 4;
}

sealed class Consumer {
    public IShape? AsShape(Square square) => square as IShape;
}
