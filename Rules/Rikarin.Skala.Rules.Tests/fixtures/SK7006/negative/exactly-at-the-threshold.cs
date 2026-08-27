// ⚠ Six levels exactly. The rule fires over the threshold, not at it.
public sealed class ExactlyAtTheThreshold {
    public static int Count(bool[] flags) {
        var total = 0;
        if (flags[0]) {
            if (flags[1]) {
                if (flags[2]) {
                    if (flags[3]) {
                        if (flags[4]) {
                            if (flags[5]) {
                                total++;
                            }
                        }
                    }
                }
            }
        }
        return total;
    }
}
