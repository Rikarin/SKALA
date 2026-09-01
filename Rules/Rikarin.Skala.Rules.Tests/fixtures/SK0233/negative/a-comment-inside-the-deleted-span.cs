using System;

public static class Annotated {
    // The fix deletes the argument list wholesale, so the note inside it would go too.
    [Obsolete( /* deliberately no message; see issue 42 */ )]
    public static int Old() => 0;
}
