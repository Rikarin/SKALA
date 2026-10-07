// A sealed record's positional properties are init-only, so a call among the arguments cannot change
// what the copy reads; a record struct is copied safely where the arguments ahead of a carried member
// run no code, or where every carried member comes first. Pinned by Probe (#425).
public sealed record Person(string Name, int Age);

public record struct Point(int X, int Y);

public static class Probe {
    static int calls;

    static string Rename(Person person) {
        calls++;
        return person.Name + "!";
    }

    static int Shift() {
        calls++;
        return 10;
    }

    public static string Run() {
        var person = new Person("a", 1);
        var renamed = new Person(Rename(person), person.Age);
        var point = new Point(1, 2);
        var moved = new Point(point.X + 1, point.Y);
        var shifted = new Point(point.X, Shift());
        return renamed + "/" + moved + "/" + shifted + "/" + calls;
    }
}
