namespace Serilog
    .Configuration;

using System;

public class Foo {
    public int Bar { get; set; }

    void M() {
        Console.WriteLine(Bar);
    }
}
