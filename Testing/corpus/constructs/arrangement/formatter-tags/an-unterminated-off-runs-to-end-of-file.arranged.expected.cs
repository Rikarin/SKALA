// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaCleanup generated=2026-09-02
namespace FormatterTags;

public class AnUnterminatedOffRunsToEndOfFile {
    public List<int> Before() => new();

    // @formatter:off
    public  int  After( ) => 1;
    public List<int> Also() => new();
}
