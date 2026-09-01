sealed class Store {
    bool ready = false;

    public void Open() => ready = true;

    public bool Ready => ready;
}
