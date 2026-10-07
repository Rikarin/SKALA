// An indexer setter on a struct-typed capture writes the capture; `readonly struct Board` is CS9117.
struct Pair {
    int first;
    int second;

    public int this[int index] {
        get => index == 0 ? first : second;
        set {
            if (index == 0) {
                first = value;
            } else {
                second = value;
            }
        }
    }
}

struct Board(Pair cells) {
    public void Mark(int index) => cells[index] = 1;

    public int First => cells[0];
}
