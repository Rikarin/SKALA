struct Counter {
    int count;

    public readonly int Read() => count;

    public void Increment() => count++;
}
