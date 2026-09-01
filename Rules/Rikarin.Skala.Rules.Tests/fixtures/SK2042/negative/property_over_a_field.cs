sealed class Person {
    readonly string name;

    public Person(string name) => this.name = name;

    public string Name => name;

    public override bool Equals(object? other) => other is Person person && person.Name == Name;

    public override int GetHashCode() => name.GetHashCode();
}
