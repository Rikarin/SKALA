using System;

public sealed class Renderer : IDisposable {
    public string Name { get; set; } = "";

    public int Budget { get; set; }

    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ The hoist rebuilds the assignments from their expressions, and a comment between two
    // members is not part of any expression — it would go out with the braces, silently, under a
    // fix the catalogue marks safe. The shape is right and the finding is withheld anyway.
    public void Draw() {
        using var renderer = new Renderer {
            Name = "irradiance",

            // Read off the field rather than written down; a later pass renumbers these.
            Budget = 64
        };

        Console.WriteLine(renderer.Name);
    }
}
