// ⚠ A shift under a bitwise operator is bit packing, and it is declined. `key << 8 | digest[i]` is
// how every byte of every buffer has ever been assembled: the shift is visibly the thing being
// placed and the bitwise operator is visibly the thing placing it. Measured rather than reasoned —
// the rule without this exclusion reports this shape once on Skala's own tree, in
// `CorpusSample.KeyOf`, and once in `pathological/operators-crammed-together.cs`, and both are the
// idiom rather than the hazard.
class C {
    ulong Pack(byte[] digest) {
        ulong key = 0;
        for (var i = 0; i < 8; i++) {
            key = key << 8 | digest[i];
        }

        return key;
    }

    int Masked(int value, int offset, int mask) => value << offset & mask;
}
