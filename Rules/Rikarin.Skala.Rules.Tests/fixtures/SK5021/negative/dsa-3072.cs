using System.Security.Cryptography;

// DSA above the floor. Whether DSA is a good choice is a different question and a different rule.
public static class Signing {
    public static DSA Signer() => DSA.Create(3072);
}
