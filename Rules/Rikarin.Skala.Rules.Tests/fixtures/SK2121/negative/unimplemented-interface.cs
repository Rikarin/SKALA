// `Base` does not implement `IShape` and is not sealed, so a subclass may. The conversion is
// explicit and the question is real.
interface IShape {
    int Sides { get; }
}

class Base { }

sealed class Consumer {
    public IShape? AsShape(Base value) => value as IShape;
}
