readonly struct Metre {
    readonly int value;

    public Metre(int value) => this.value = value;

    public int Read() => value;

    public int Value => value;
}
