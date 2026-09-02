using System.IO;

// The replacement has to be parenthesised here: `!source is Stream` parses as `(!source) is Stream`.
class Negated {
    public bool Test(object source) => !typeof(Stream).IsInstanceOfType(source);
}
