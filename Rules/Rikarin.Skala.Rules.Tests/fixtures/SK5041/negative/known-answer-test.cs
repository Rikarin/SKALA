using System;
using System.Security.Cryptography;
using System.Text;

// ⚠ Load-bearing rather than a courtesy: RFC 6070's PBKDF2 test vectors specify the salt as the
// literal string "salt". Without the test-method exemption this rule would fail the build of
// every crypto library that checks itself against the standard's own vectors, at `error`.
public sealed class FactAttribute : Attribute {
}

public static class Assert {
    public static void Equal(byte[] expected, byte[] actual) {
    }
}

public sealed class Rfc6070Vectors {
    [Fact]
    public void Vector_One() {
        var expected = Convert.FromHexString("0c60c80f961f0e71f3a9b524af6012062fe037a6");
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            "password",
            Encoding.UTF8.GetBytes("salt"),
            1,
            HashAlgorithmName.SHA1,
            20
        );

        Assert.Equal(expected, actual);
    }
}
