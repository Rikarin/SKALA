namespace Contoso.Design;

// ⚠ The access that is legal runs *inward*: a nested type reaches its container's private members.
// The reverse — a container calling a nested type's private constructor — is CS0122, which is why
// the reachability scan reads the file rather than the candidate's own body.
public sealed class Pipeline {
    private Pipeline(int stages) => Stages = stages;

    public int Stages { get; }

    public sealed class Builder {
        public int Count { get; set; }

        public Pipeline Build() => new Pipeline(Count);
    }
}
