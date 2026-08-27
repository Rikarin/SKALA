// Eight levels of nesting against a default threshold of six.
public sealed class EightLevels {
    public static int Count(bool[] flags) {
        var total = 0;
        if (flags[0]) {
            if (flags[1]) {
                if (flags[2]) {
                    if (flags[3]) {
                        if (flags[4]) {
                            if (flags[5]) {
                                if (flags[6]) {
                                    if (flags[7]) {
                                        total++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return total;
    }
}
