using System.Security.Cryptography;

// Read at run time from settings, so there is no compile-time constant.
public sealed class Options {
    public int Bits { get; init; }
}

public static class Signing {
    public static RSA Signer(Options options) {
        var signer = RSA.Create();
        signer.KeySize = options.Bits;
        return signer;
    }
}
