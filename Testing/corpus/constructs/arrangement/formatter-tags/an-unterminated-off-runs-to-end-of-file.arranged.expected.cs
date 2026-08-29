// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaCleanup generated=2026-08-29
namespace FormatterTags;

public class AnUnterminatedOffRunsToEndOfFile {
    public List<int> Before() => new();

    // @formatter:off
    public  int  After( ) => 1;
    public List<int> Also() => new();
}
