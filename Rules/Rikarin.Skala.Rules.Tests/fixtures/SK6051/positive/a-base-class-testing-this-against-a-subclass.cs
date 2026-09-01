namespace Contoso.Design;

public class Shape {
    public double Area() {
        if (this is Circle) {
            return 3.14159;
        }

        return 0;
    }
}

public sealed class Circle : Shape;
