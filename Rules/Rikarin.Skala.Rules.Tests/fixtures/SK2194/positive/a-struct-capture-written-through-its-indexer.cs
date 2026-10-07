// An indexer setter on a struct-typed capture runs against the capture's own storage (CS9117).
namespace Fixtures {
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

    sealed class Board(Pair cells) {
        public void Mark(int index) => cells[index] = 1;

        public int First => cells[0];
    }
}
