// In a positional record the parameter is also where the property is written down, both symbols
// point at the same ParameterSyntax, and a name in a member body resolves to the property. That
// is a different analysis with a different answer; the exclusion here is structural, because a
// record is a different syntax node and this analyzer never sees one.
namespace Fixtures {
    record Point(int X, int Y) {
        public int Sum => X + Y;
    }

    record struct Cell(int Row) {
        public int Doubled => Row * 2;
    }
}
