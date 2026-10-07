// Two `bool` conditions: `&&` evaluates the second only when the first is true, exactly as the nested
// `if` does, and in the same order. Pinned by Probe (#425).
public static class Probe {
    static string log = "";

    static bool Left(bool answer) {
        log += "left;";
        return answer;
    }

    static bool Right(bool answer) {
        log += "right;";
        return answer;
    }

    static void Go(bool first, bool second) {
        if (Left(first)) {
            if (Right(second)) {
                log += "both;";
            }
        }
    }

    public static string Run() {
        Go(false, true);
        Go(true, false);
        Go(true, true);
        return log;
    }
}
