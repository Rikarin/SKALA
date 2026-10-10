// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-10
// #596's residue, SK-DIV-0447: a conditional's `=` before a call condition that fits below breaks only while
// 9 · (the call's end below) + 2 · (the `=`'s end) + 64 · (its argument count) ≤ 1136; past it the call chops.

class C596r {
    void M1() {
        var vvvvvvvvvvvv = Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
            ? Cast<object>(first, second)
            : context;
    }

    void M2() {
        var vvvvvvvvvvvv =
            Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                ? Cast<object>(first, second)
                : context;
    }

    void M3() {
        var vvvvvvvvvvvvvvvvvv = Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
            ? Cast<object>(first, second)
            : context;
    }

    void M4() {
        var vvvvvvvvvvvvvvvvvv =
            Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                ? Cast<object>(first, second)
                : context;
    }

    void M5() {
        var vvvvvvvvvvvvvvvvvvvvvvvv = Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
            ? Cast<object>(first, second)
            : context;
    }

    void M6() {
        var vvvvvvvvvvvvvvvvvvvvvvvv =
            Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                ? Cast<object>(first, second)
                : context;
    }

    void M7() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                ? Cast<object>(first, second)
                : context;
    }

    void M8() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Select(
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
        )
            ? Cast<object>(first, second)
            : context;
    }

    void M9() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                ? Cast<object>(first, second)
                : context;
    }

    void M10() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Select(
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
        )
            ? Cast<object>(first, second)
            : context;
    }

    void M11() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                ? Cast<object>(first, second)
                : context;
    }

    void M12() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Select(
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
        )
            ? Cast<object>(first, second)
            : context;
    }

    void M13() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                ? Cast<object>(first, second)
                : context;
    }

    void M14() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Select(
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
        )
            ? Cast<object>(first, second)
            : context;
    }

    void M15() {
        if (x) {
            bool vvvvvvvvvvvvvvvvvvvvvv =
                ComputeIt(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbb, cccccccccccccccccc) ? first : second;
        }
    }

    void M16() {
        if (x) {
            bool vvvvvvvvvvvvvvvvvvvvvv = ComputeIt(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbb, ccccccccccccccccccc)
                ? first
                : second;
        }
    }

    void M17() {
        if (x) {
            bool vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                ComputeIt(aaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccc) ? first : second;
        }
    }

    void M18() {
        if (x) {
            bool vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ComputeIt(
                aaaaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbb,
                ccccccccccccccccccccc
            )
                ? first
                : second;
        }
    }

    void M19() {
        if (x) {
            bool vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                ComputeIt(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbb, cccccccccccccccccc) ? first : second;
        }
    }

    void M20() {
        if (x) {
            bool vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ComputeIt(
                aaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbb,
                ccccccccccccccccccc
            )
                ? first
                : second;
        }
    }

    void M21() {
        string vvvv = Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
            ? first
            : second;
    }

    void M22() {
        string vvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
                ? first
                : second;
    }

    void M23() {
        string vvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
                ? first
                : second;
    }

    void M24() {
        string vvvv = Fn(
            zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M25() {
        string vvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz) ? first : second;
    }

    void M26() {
        string vvvvvvvvvvvvv = Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
            ? first
            : second;
    }

    void M27() {
        string vvvvvvvvvvvvv = Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
            ? first
            : second;
    }

    void M28() {
        string vvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
                ? first
                : second;
    }

    void M29() {
        string vvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
                ? first
                : second;
    }

    void M30() {
        string vvvvvvvvvvvvv = Fn(
            zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M31() {
        string vvvvvvvvvvvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz) ? first : second;
    }

    void M32() {
        string vvvvvvvvvvvvvvvvvvvvvv = Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
            ? first
            : second;
    }

    void M33() {
        string vvvvvvvvvvvvvvvvvvvvvv = Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
            ? first
            : second;
    }

    void M34() {
        string vvvvvvvvvvvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz) ? first : second;
    }

    void M35() {
        string vvvvvvvvvvvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
                ? first
                : second;
    }

    void M36() {
        string vvvvvvvvvvvvvvvvvvvvvv = Fn(
            zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M37() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
                ? first
                : second;
    }

    void M38() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
            zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M39() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
                ? first
                : second;
    }

    void M40() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
            zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M41() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Fn(zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz) ? first : second;
    }

    void M42() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
            zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M43() {
        string vvvvvvvvvvvvv =
            Fn(aaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbb, cccccccccccccccc, zzzzzzzzzzzzzzzz) ? first : second;
    }

    void M44() {
        string vvvvvvvvvvvvv = Fn(aaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbb, cccccccccccccccc, zzzzzzzzzzzzzzzzzzz)
            ? first
            : second;
    }

    void M45() {
        string vvvvvvvvvvvvvvvvvvvvvv =
            Fn(aaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbb, cccccccccccccccc, zzzzzzzzzzzzzzzz) ? first : second;
    }

    void M46() {
        string vvvvvvvvvvvvvvvvvvvvvv = Fn(aaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbb, cccccccccccccccc, zzzzzzzzzzzzzzzzzzz)
            ? first
            : second;
    }

    void M47() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Fn(aaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbb, cccccccccccccccc, zzzzzzzzzzzzzzzz) ? first : second;
    }

    void M48() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
            aaaaaaaaaaaaaaaa,
            bbbbbbbbbbbbbbbb,
            cccccccccccccccc,
            zzzzzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M49() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Fn(aaaaaaaaaaaaaaa, bbbbbbbbbbbbbbb, ccccccccccccccc, zzzzzzzzzzzzzzzz) ? first : second;
    }

    void M50() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
            aaaaaaaaaaaaaaaa,
            bbbbbbbbbbbbbbbb,
            cccccccccccccccc,
            zzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }

    void M51() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
            Fn(aaaaaaaaaaaaa, bbbbbbbbbbbbb, ccccccccccccc, zzzzzzzzzzzzzzzz) ? first : second;
    }

    void M52() {
        string vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
            aaaaaaaaaaaaaa,
            bbbbbbbbbbbbbb,
            cccccccccccccc,
            zzzzzzzzzzzzzzzz
        )
            ? first
            : second;
    }
}
