// ⚠ #397: `property_over_a_field.cs` with `Name` split into a partial definition and implementation.
// The type's member list holds the definition, whose `{ get; }` has no getter to read, so `Name` and
// the `name` it returns looked like two pieces of state and the hash code over `name` was reported as
// reading something equality ignores. The implementation is where the getter is.
sealed partial class Person {
    readonly string name;

    public Person(string name) => this.name = name;

    public partial string Name { get; }

    public partial string Name => name;

    public override bool Equals(object? other) => other is Person person && person.Name == Name;

    public override int GetHashCode() => name.GetHashCode();
}
