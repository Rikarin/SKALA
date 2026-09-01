using System.Collections.Generic;

public abstract class Animal {
    public abstract int Legs { get; }
}

public sealed class Dog : Animal {
    public override int Legs => 4;
}

public static class Kennel {
    public static int TotalLegs(List<Dog> dogs) {
        var total = 0;
        foreach (Animal animal in dogs) {
            total += animal.Legs;
        }

        return total;
    }
}
