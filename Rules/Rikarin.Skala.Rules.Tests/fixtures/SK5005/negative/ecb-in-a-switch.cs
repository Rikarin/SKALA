using System.Security.Cryptography;

public static class Describe {
    public static string Of(CipherMode mode) =>
        mode switch {
            CipherMode.ECB => "leaks block structure",
            CipherMode.CBC => "needs a random IV",
            _ => "other"
        };
}
