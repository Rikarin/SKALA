// The call could read the field without naming it, so the initialized value may be observable.
public sealed class Registry {
    int slots = 4;

    public Registry(int given) {
        Prepare();
        slots = given;
    }

    void Prepare() {
        System.Console.WriteLine(slots);
    }
}
