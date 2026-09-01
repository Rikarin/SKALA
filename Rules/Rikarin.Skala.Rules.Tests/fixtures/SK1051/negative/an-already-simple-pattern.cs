public sealed class Gate {
    public bool Small(int count) => count is <= 5;

    public bool Open(int value) => value is > 0;
}
