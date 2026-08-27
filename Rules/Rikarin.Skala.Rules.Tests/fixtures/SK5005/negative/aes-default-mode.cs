using System.Security.Cryptography;

public static class Box {
    public static SymmetricAlgorithm Make() => Aes.Create();
}
