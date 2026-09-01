public sealed class Reader {
    int remaining = 3;

    bool Next(out int value) {
        value = remaining--;
        return value > 0;
    }

    public int Sum() {
        var total = 0;
        int value;
        while (Next(out value)) {
            total += value;
        }

        return total;
    }
}
