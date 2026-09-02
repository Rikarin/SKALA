// ⚠ Any exit at all withdraws the finding, reachable or not. Telling somebody their loop hangs
// when a `return` two branches down ends it is a wrong finding rather than a noisy one, so the check
// over-bails on purpose.
class C {
    void Returns(int count) {
        var i = 0;
        while (i < count) {
            if (Ready(i)) {
                return;
            }
        }
    }

    void Breaks(int count) {
        var i = 0;
        while (i < count) {
            if (Ready(i)) {
                break;
            }
        }
    }

    void Throws(int count) {
        var i = 0;
        while (i < count) {
            throw new System.InvalidOperationException();
        }
    }

    static bool Ready(int i) => i > 2;
}
