using System.Collections.Generic;

public interface IShape {
    double Area { get; }
}

public sealed class Circle : IShape {
    public double Radius { get; init; }

    public double Area => Radius * Radius;
}

public static class Shapes {
    public static double TotalArea(List<Circle> circles) {
        var total = 0.0;
        foreach (IShape shape in circles) {
            total += shape.Area;
        }

        return total;
    }
}
