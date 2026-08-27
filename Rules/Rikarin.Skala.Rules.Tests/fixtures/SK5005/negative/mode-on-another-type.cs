using System.Security.Cryptography;

// A `Mode` property on something that is not a cipher, even assigned the same enum member.
public sealed class Renderer {
    public CipherMode Mode { get; set; }
}

public static class Use {
    public static Renderer Make() {
        var renderer = new Renderer();
        renderer.Mode = CipherMode.ECB;
        return renderer;
    }
}
