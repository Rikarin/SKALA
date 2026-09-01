using System;

// Two declarators, neither initialized: the attribute is doing its job on both.
static class Slots {
    [ThreadStatic] static int first, second;

    public static int Sum => first + second;
}
