// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaCleanup generated=2026-08-27
namespace FormatterTags;

public class AnUnterminatedOffRunsToEndOfFile {
    public List<int> Before() => new();

    // @formatter:off
    public  int  After( ) => 1;
    public List<int> Also() => new();
}
