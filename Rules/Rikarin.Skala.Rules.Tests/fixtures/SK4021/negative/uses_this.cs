sealed class UsesThisFixture {
    public int Use() => Describe();

    int Describe() => this.GetHashCode();
}
