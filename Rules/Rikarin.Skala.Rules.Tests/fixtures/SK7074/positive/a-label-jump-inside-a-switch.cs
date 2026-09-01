// A `goto` to a label is reported wherever it is written. The label is not confined to the switch,
// so this is not the fall-through form the rule stays quiet about.
public sealed class Work {
    public int Run(int value) {
        switch (value) {
            case 0:
                goto done;
            default:
                return value;
        }

    done:
        return -1;
    }
}
