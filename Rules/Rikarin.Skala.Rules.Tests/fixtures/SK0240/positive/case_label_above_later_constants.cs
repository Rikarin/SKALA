// Every label below is a constant, and two constant labels for one value would be CS0152, so no later
// section can take the 2 that `case 2:` names: it reaches `default:` with or without the label.
public static class Probe {
    static string Classify(int value) {
        switch (value) {
            case 2:
            default:
                return "default";
            case 3:
                return "three";
        }
    }

    public static string Run() => Classify(2) + " " + Classify(3) + " " + Classify(4);
}
