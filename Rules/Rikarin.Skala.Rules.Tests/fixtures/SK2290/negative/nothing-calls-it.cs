// ⚠ The guard that separates a rule from a noise generator. "Every caller discards it" is vacuously
// true of a method nobody calls, and without the zero-call-site refusal this rule would fire on every
// uncalled `private` helper in every tree. An uncalled private member is a different finding with a
// different repair, and it is not this one.
class Reader {
    static bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }
}
