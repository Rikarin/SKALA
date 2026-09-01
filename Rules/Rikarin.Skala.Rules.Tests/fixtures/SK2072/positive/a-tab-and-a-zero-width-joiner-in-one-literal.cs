// Two findings in one literal, and the second is not a duplicate of the first: each
// character carries its own edit, so `skala fix` repairs both in one pass.
// contains: U+0009
// contains: U+200C
namespace Fixtures;

sealed class Columns {
    public const string Row = "left	right‌edge";
}
