namespace Contoso.Design;

// A `static readonly` field in an interface is a different declaration with different initialization
// rules, so the one-token edit would not be the repair it claims to be.
public interface IProtocol {
    const int Version = 2;

    int Read();
}
