readonly struct Metre {
    readonly int value;

    public Metre(int value) => this.value = value;

    public readonly int Read() => value;
}
