// ⚠ Two draws, not one expression twice. Structural equality alone would report this.
using System;

class C {
    bool M(Random rng) => rng.Next() == rng.Next();

    int N(System.IO.TextReader reader) => reader.Read() - reader.Read();
}
