// ⚠ The read-assign-test idiom. The assignment is an operand of the comparison, not the condition,
// and this is the single most common correct use of assignment inside a condition in all of C#.
using System.IO;

class C {
    void M(TextReader reader) {
        string? line;
        while ((line = reader.ReadLine()) != null) {
            Handle(line);
        }
    }

    static void Handle(string line) { }
}
