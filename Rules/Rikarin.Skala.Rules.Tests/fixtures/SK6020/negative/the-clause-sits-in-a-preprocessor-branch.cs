using System;

public static class Conditional {
    public static int Rank<T>(T value)
#if SKALA_ENUM_ONLY
        where T : Enum
#endif
        => 0;
}
