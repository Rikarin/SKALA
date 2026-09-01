sealed class InstanceReceiverFixture {
    public int Use(InstanceReceiverFixture other, int value) => other.Twice(value);

    int Twice(int value) => value * 2;
}
