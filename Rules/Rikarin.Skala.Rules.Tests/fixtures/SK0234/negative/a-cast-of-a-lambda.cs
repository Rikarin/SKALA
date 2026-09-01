using System;

public static class Lambdas {
    // A lambda has no type of its own, so there is nothing for an identity conversion to compare.
    public static Action Nothing() => (Action)(() => { });
}
