using System.IO;

// A question about two types with no value in it. There is nothing for `is` to test.
class TwoTypes {
    public bool Test() => typeof(Stream).IsAssignableFrom(typeof(MemoryStream));
}
