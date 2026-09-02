using System.Security.Cryptography;

// ⚠ The verifying side must re-derive with the salt that was stored beside the hash. It is a
// variable by necessity, so the rule is silent here by construction — it reports the code that
// creates a bad credential, not the code that reads one back.
public static class Credentials {
    public static bool Verify(string password, byte[] storedSalt, byte[] storedKey) {
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, storedSalt, 600_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(derived, storedKey);
    }
}
