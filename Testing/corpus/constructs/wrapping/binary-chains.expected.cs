// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class BinaryChains {
    bool Fits(int a, int b) => a > 0 && b > 0;

    bool DoesNotFit(int a, int b, int c, int d) {
        if (a > 0
            && b > 0
            && c > 0
            && d > 0
            && a < 100
            && b < 100
            && c < 100
            && d < 100
            && a != b
            && c != d
            && a != d) {
            return true;
        }

        return false;
    }

    bool Pattern(object o) {
        return o is int
            or long
            or short
            or byte
            or uint
            or ulong
            or ushort
            or sbyte
            or float
            or double
            or decimal
            or char;
    }

    bool KeepsTheAuthorsOneBreak(bool a, bool b, bool c) {
        return a && b
            || c;
    }
}
