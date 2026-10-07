// A writable `ref` local aliases the captured parameter; `readonly struct Gauge` is CS9116.
struct Gauge(int level) {
    public void Fill() {
        ref var slot = ref level;
        slot = 100;
    }

    public int Level => level;
}
