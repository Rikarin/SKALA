// ⚠ Two reads, not one expression twice. Structural equality alone would report this.
using System;

class C {
    int M(System.IO.TextReader reader) => reader.Read() - reader.Read();

    bool N(Random rng) => rng.NextDouble() > 0.5 && rng.NextDouble() > 0.5;
}
