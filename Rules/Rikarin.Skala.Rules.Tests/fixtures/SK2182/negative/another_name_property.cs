sealed class Person {
    public string Name { get; set; } = string.Empty;
}

static class Route {
    // A `Name` that is not `Type.Name`.
    public static bool IsAda(Person person) => person.Name == "Ada";
}
