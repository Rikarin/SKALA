public sealed class State {
    public int Total;
}

public sealed class Ledger {
    readonly State state = new();

    public void Record(int amount) {
        this.state.Total = this.state.Total - amount;
    }
}
