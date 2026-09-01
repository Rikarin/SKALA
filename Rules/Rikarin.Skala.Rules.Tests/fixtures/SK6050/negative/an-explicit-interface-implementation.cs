namespace Contoso.Design;

public interface IQuota {
    int Remaining(string tenant);
}

// ⚠ An explicit interface implementation has `Accessibility.Private` at the symbol layer and is the
// exact shape this rule must never report: the signature is somebody else's, and a constant answer
// is a legitimate implementation of it.
public sealed class Unlimited : IQuota {
    int IQuota.Remaining(string tenant) => int.MaxValue;
}
