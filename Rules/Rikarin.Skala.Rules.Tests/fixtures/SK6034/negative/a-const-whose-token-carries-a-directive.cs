namespace Contoso.Design;

// The token the fix names is not the token every branch compiles, so the rule says nothing.
public static class Limits {
    public
#if NET10_0_OR_GREATER
        const
#else
        const
#endif
        int MaxRetries = 3;
}
