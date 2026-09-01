public interface IOrderClass {
    int Id { get; }
}

public enum ShapeStruct {
    Point,
    Line
}

public delegate void HandleClass(int id);
