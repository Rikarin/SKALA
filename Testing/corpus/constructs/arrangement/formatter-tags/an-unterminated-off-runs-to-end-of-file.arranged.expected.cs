// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaCleanup generated=2026-09-04
namespace FormatterTags;

public class AnUnterminatedOffRunsToEndOfFile {
    public List<int> Before() => new();

    // @formatter:off
    public  int  After( ) => 1;
    public List<int> Also() => new();
}
