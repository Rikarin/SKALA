public sealed class Codes {
    public int[] All(int fallback) => [
        .. new[] {
            200,
            204
#if DEBUG
        }
#else
        }
#endif
        ,
        fallback
    ];
}
