public sealed class Buffer {
    int size = 16;

    public Buffer() {
        size = 32;
    }

    public Buffer(int given) {
        size = given;
    }

    public int Size => size;
}
