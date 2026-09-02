using System.Security.Cryptography;

// ⚠ `= new byte[16]` on a field is the allocate-then-fill shape, and the static constructor is what
// fills it. The field case deliberately requires an explicit list of literals for this reason.
public static class Sealer {
    static readonly byte[] Buffer = new byte[16];

    static Sealer() => RandomNumberGenerator.Fill(Buffer);

    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Buffer;
        return cipher.CreateEncryptor();
    }
}
