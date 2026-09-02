using System.Security.Cryptography;
using System.Text;

public static class Credentials {
    public static byte[] Derive(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            Encoding.UTF8.GetBytes("my-application-salt"),
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
}
