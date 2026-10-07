using System;
using System.Security.Cryptography;
using System.Text;

// #400: RFC 6070's vector again, as a partial method whose `[Fact]` is on the definition. The
// implementation's body is a test method's body.
public sealed class FactAttribute : Attribute {
}

public static class Assert {
    public static void Equal(byte[] expected, byte[] actual) {
    }
}

public sealed partial class Rfc6070Vectors {
    [Fact]
    public partial void Vector_One();

    public partial void Vector_One() {
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
