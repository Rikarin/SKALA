using Rikarin.Skala.Analysis.Loading;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #517's compiler path, read off a recorded <c>Csc</c> command line in every shape a build writes it.
/// </summary>
/// <remarks>
///     ⚠ The Windows CI runners record the host unquoted under <c>C:\Program Files</c>, so splitting the line
///     first made the first token <c>C:\Program</c> and no path was ever kept there — which
///     <c>MultiTargetedBinlog_GroupsTheMonikersTheSameWayTheWorkspaceDoes</c> caught on Windows only.
/// </remarks>
public sealed class CompilerPathSplitTests {
    [Theory]
    [InlineData(
        "/usr/share/dotnet/sdk/10.0.400/Roslyn/bincore/csc /noconfig a.cs",
        "/usr/share/dotnet/sdk/10.0.400/Roslyn/bincore/csc"
    )]
    [InlineData(@"C:\sdk\Roslyn\bincore\csc.exe /noconfig a.cs", @"C:\sdk\Roslyn\bincore\csc.exe")]
    [InlineData(
        @"C:\Program Files\dotnet\dotnet.exe exec ""C:\Program Files\dotnet\sdk\10.0.400\Roslyn\bincore\csc.dll"" /noconfig a.cs",
        @"C:\Program Files\dotnet\sdk\10.0.400\Roslyn\bincore\csc.dll"
    )]
    [InlineData(
        @"C:\Program Files\dotnet\dotnet.exe exec C:\Program Files\dotnet\sdk\10.0.400\Roslyn\bincore\csc.dll /noconfig a.cs",
        @"C:\Program Files\dotnet\sdk\10.0.400\Roslyn\bincore\csc.dll"
    )]
    [InlineData(
        @"""C:\Program Files\dotnet\sdk\10.0.400\Roslyn\bincore\csc.exe"" /noconfig a.cs",
        @"C:\Program Files\dotnet\sdk\10.0.400\Roslyn\bincore\csc.exe"
    )]
    [InlineData(
        "/home/u/.nuget/packages/microsoft.net.compilers.toolset/4.11.0/tasks/netcore/bincore/csc.dll /noconfig a.cs",
        "/home/u/.nuget/packages/microsoft.net.compilers.toolset/4.11.0/tasks/netcore/bincore/csc.dll"
    )]
    public void TheCompilersPath_IsKept_AndOnlyArgumentsRemain(string line, string path) {
        var (compiler, arguments) = BinlogLoader.SplitCompiler(line);

        Assert.Equal(path, compiler);
        Assert.Equal("/noconfig a.cs", arguments.Trim());
    }

    [Fact]
    public void ALineWithNoCompiler_IsReturnedWhole() {
        var (compiler, arguments) = BinlogLoader.SplitCompiler("/noconfig /r:x/csc.dll a.cs");

        Assert.Equal(string.Empty, compiler);
        Assert.Equal("/noconfig /r:x/csc.dll a.cs", arguments);
    }
}
