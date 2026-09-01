sealed class Store {
    int capacity = 8;

    public void Grow() => capacity++;

    public int Capacity => capacity;
}
