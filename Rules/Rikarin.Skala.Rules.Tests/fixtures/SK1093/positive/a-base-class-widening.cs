public class Animal { }

public sealed class Dog : Animal { }

public sealed class Pens {
    public Animal Get() {
        var pet = (Animal)new Dog();
        return pet;
    }
}
