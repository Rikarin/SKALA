// `Root` is initialized after `Path`, so `Path` is built from null and reads "/cache" forever.
static class Defaults {
    public static readonly string Path = Root + "/cache";
    public static readonly string Root = "/var/lib";
}
