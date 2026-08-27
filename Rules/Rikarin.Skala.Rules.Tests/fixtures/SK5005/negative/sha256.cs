using System.Security.Cryptography;

public static class Digest {
    public static byte[] Of(byte[] input) => SHA256.HashData(input);
}
