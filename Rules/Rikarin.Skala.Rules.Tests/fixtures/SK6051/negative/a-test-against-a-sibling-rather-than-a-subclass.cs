using System.IO;

namespace Contoso.Design;

// `StringReader` and `Reader` share a base and neither derives from the other, so nothing here is a
// type asking about its own subclasses. The finding requires the tested type to derive from the type
// of `this`, which is what separates the design smell from an ordinary type test.
public class Reader : TextReader {
    public bool IsStringReader() => this is StringReader;
}
