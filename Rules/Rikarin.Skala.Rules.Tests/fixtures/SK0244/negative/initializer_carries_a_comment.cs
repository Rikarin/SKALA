sealed class Store {
    int capacity = /* the serializer round-trips this and needs it written */ 0;

    public void Grow() => capacity++;

    public int Capacity => capacity;
}
