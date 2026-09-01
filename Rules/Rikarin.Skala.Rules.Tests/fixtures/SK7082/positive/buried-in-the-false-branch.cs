// ⚠ The `false` branch is free only when the next conditional *is* it. Here one is buried inside a
// call in the false branch, which is not a ladder rung: the reader leaves the top-to-bottom reading
// to work out an argument, which is the thing the exemption is not for.
namespace Fixtures;

class Buried {
    static string Wrap(string value) => value;

    public static string Describe(bool a, bool b) => a ? "yes" : Wrap(b ? "maybe" : "no");
}
