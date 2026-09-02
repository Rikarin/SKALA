using System;

static class Load {
    // A different `GetType` entirely: the one-argument static lookup on `Type`.
    public static Type? Named(string name) => Type.GetType(name);
}
