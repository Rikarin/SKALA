// ⚠ The rule's stated position: `goto case` is the only way C# expresses switch fall-through, it
// cannot leave the switch it is written in, and the flow it describes is on the screen already.
public sealed class Work {
    public int Run(int value) {
        switch (value) {
            case 0:
                goto case 1;
            case 1:
                return 1;
            default:
                return value;
        }
    }
}
