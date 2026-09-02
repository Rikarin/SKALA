// Spelling the type out changes nothing about the order the initializers run in.
static class Registry {
    public static readonly int Capacity = Registry.Requested * 2;
    public static readonly int Requested = 16;
}
