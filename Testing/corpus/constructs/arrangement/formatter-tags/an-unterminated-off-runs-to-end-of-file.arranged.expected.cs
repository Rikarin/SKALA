// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaCleanup generated=2026-08-28
namespace FormatterTags;

public class AnUnterminatedOffRunsToEndOfFile {
    public List<int> Before() => new();

    // @formatter:off
    public  int  After( ) => 1;
    public List<int> Also() => new();
}
