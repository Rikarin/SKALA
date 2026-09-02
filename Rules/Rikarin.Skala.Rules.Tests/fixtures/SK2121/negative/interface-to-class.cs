// Going the other way — from the interface to one of its implementations — is a narrowing, and
// another implementation of the same interface makes it a question worth asking.
interface IShape {
    int Sides { get; }
}

sealed class Square : IShape {
    public int Sides => 4;
}

sealed class Triangle : IShape {
    public int Sides => 3;
}

sealed class Consumer {
    public Square? AsSquare(IShape shape) => shape as Square;
}
