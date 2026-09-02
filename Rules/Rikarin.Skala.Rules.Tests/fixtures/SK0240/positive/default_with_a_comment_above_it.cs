// ⚠ #302, the `default:` half. The comment is leading trivia of the `default` keyword and
// `section.Span` starts at that keyword, so the fix leaves the comment standing.
class C {
    public static void Run(int value) {
        switch (value) {
            case 1:
                Use(value);
                break;

            // Everything else is handled by the outer dispatcher.
            default:
                break;
        }
    }

    static void Use(int value) { }
}
