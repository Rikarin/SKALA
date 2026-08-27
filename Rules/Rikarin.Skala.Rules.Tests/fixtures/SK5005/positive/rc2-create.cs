using System.Security.Cryptography;

public static class Legacy {
    public static SymmetricAlgorithm Make() => RC2.Create();
}
