sealed class Store {
    int capacity = 0;

    public void Grow() => capacity++;

    public int Capacity => capacity;
}
