public sealed class Counter {
    int served;

    public void Serve() {
        // Also not atomic — and the author never claimed otherwise. The keyword is what turns this
        // from an ordinary field into a statement about threading that is wrong.
        served++;
    }

    public int Served => served;
}
