using System.IO;

class Commented {
    public bool Test(object source) =>
        typeof(Stream).IsInstanceOfType(/* whatever the caller handed us */ source);
}
