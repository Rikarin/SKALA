using System.Runtime.CompilerServices;

// `params` has to be last, so the caller-info run has nowhere to go: the only rewrite that
// satisfies both constraints is the one already written.
public static class Formatting {
    public static void Write([CallerMemberName] string caller = "", params object[] values) { }
}
