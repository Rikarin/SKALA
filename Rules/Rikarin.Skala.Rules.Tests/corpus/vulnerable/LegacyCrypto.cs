using System.Security.Cryptography;

namespace Corpus.Vulnerable;

/// <summary>SK5005 — every shipped spelling of a broken cipher.</summary>
public static class LegacyCrypto {
    public static SymmetricAlgorithm Des() => DES.Create();

    public static SymmetricAlgorithm TripleDes() => TripleDES.Create();

    public static SymmetricAlgorithm Rc2() => RC2.Create();

    public static SymmetricAlgorithm EcbMode() {
        var cipher = Aes.Create();
        cipher.Mode = CipherMode.ECB;
        return cipher;
    }

    public static SymmetricAlgorithm BothAtOnce() {
        var cipher = TripleDES.Create();
        cipher.Mode = CipherMode.ECB;
        return cipher;
    }
}
