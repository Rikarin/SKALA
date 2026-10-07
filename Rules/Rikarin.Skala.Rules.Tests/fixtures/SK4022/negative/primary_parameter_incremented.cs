struct Counter(int count) {
    public void Tick() => count++;

    public int Count => count;
}
