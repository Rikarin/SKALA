using System.Security.Cryptography;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
///     The configuration <c>jb cleanupcode</c> is handed: the Rider export, with <c>root = true</c>
///     prepended so that it stands alone in a scratch directory.
/// </summary>
/// <remarks>
///     ⚠ <b>This exists because the repository's own <c>.editorconfig</c> was doing two jobs.</b> Until
///     this type, the oracle harness copied <c>&lt;root&gt;/.editorconfig</c> into its scratch project.
///     That file happened to be byte-identical to <c>editor_config_template</c> plus <c>root = true</c>,
///     so the two jobs — "what Skala formats Skala with" (ADR-015) and "what ReSharper was configured
///     with" — were indistinguishable and nobody had to choose. They are not the same job, and the day
///     the repository's own file stops being spelled in ReSharper's key namespace they come apart
///     badly: <c>cleanupcode</c> does not error on a key it does not recognise, it ignores it. An oracle
///     handed a file of keys it cannot read formats with its own built-in defaults and says nothing,
///     and a corpus regenerated against it would be frozen against an unconfigured oracle with no test
///     anywhere failing to say so.
///     <para>
///         ⚠ The bytes are therefore built from the export rather than read from
///         <c>&lt;root&gt;/.editorconfig</c>, and they are the same bytes: <c>.editorconfig</c> is
///         37 685 bytes, the export is 37 672, and the 13 between them are
///         <see cref="RootDeclaration" />. So the provenance digest every fixture header records is
///         unmoved by the separation, which is the point — this is a change of which file the oracle
///         reads, not a change of what the oracle was configured with.
///     </para>
///     <para>
///         ⚠ <c>root = true</c> is not decoration. The scratch project lives under the system temp
///         directory, and without the declaration <c>cleanupcode</c>'s own EditorConfig walk keeps
///         climbing out of it into whatever the machine happens to have above it. It is also what
///         <c>ArrangementFixedPoint</c> and <c>ScratchTree</c> rely on when they write this text into a
///         per-file subdirectory.
///     </para>
/// </remarks>
public static class OracleEditorConfig {
    /// <summary>
    ///     What the repository's own <c>.editorconfig</c> adds to the export, byte for byte.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>\n</c> rather than <see cref="Environment.NewLine" />, and that is load-bearing rather
    ///     than tidy: the digest is over bytes, so a CRLF here would move it on Windows only and the
    ///     corpus would read as stale on one platform and current on another.
    /// </remarks>
    public const string RootDeclaration = "root = true\n\n";

    /// <summary>The Rider export, unmodified. Never written to.</summary>
    public static string TemplatePath { get; } =
        System.IO.Path.Combine(Corpus.RepositoryRoot, "editor_config_template");

    /// <summary>
    ///     The file the oracle is handed, materialised on disk.
    /// </summary>
    /// <remarks>
    ///     ⚠ A path rather than a string, because that is what the oracle harness needs:
    ///     <see cref="OracleRunner.Format" /> copies a file and <see cref="OracleFixture.HashConfig" />
    ///     hashes one, and the digest exists to answer "is this the same file <c>jb</c> was handed".
    ///     Hashing bytes that are never written anywhere would answer a different question.
    ///     <para>
    ///         The location is content-addressed by the digest it holds, so a stale copy cannot be
    ///         mistaken for a current one and two processes racing on it write identical bytes.
    ///     </para>
    /// </remarks>
    public static string Path { get; } = Materialise();

    /// <summary>The bytes of that file, built from the export every time it is asked for.</summary>
    public static byte[] Bytes() => [.. Encoding.UTF8.GetBytes(RootDeclaration), .. File.ReadAllBytes(TemplatePath)];

    /// <summary>The same content as text, for the callers that append overrides to it.</summary>
    public static string Text() => RootDeclaration + File.ReadAllText(TemplatePath);

    /// <summary>
    ///     Returns <paramref name="path" />, having refused the one file the oracle must never be
    ///     handed.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A runtime refusal rather than a test, because the failure it prevents is silent.</b>
    ///     <c>cleanupcode</c> does not reject a configuration whose keys it cannot read — it ignores
    ///     them and formats with its built-in defaults. So a call site repointed at
    ///     <see cref="Corpus.RepositoryEditorConfigPath" /> would produce a complete, plausible,
    ///     entirely unconfigured corpus, and every downstream check compares committed bytes against
    ///     committed bytes and would stay green. There is no later point at which this gets caught.
    ///     <para>
    ///         It is deliberately a check on <em>which file</em> and not on <em>which keys</em>. Today
    ///         the two files are byte-identical, so a key-shaped check would pass on both and assert
    ///         nothing; the rule being encoded is that the oracle reads the export, whatever the
    ///         repository's own configuration happens to say this week.
    ///     </para>
    /// </remarks>
    public static string Reading(string path) {
        if (string.Equals(
                System.IO.Path.GetFullPath(path),
                System.IO.Path.GetFullPath(Corpus.RepositoryEditorConfigPath),
                StringComparison.Ordinal
            )) {
            throw new InvalidOperationException(
                "refusing to run `jb cleanupcode` under "
                + Corpus.RepositoryEditorConfigPath
                + ". That file is Skala's own configuration (ADR-015) and is free to be spelled in a key "
                + "namespace ReSharper has never heard of; `cleanupcode` would ignore every such key "
                + "without a word and format with its own defaults. The oracle reads "
                + Path
                + " — see OracleEditorConfig."
            );
        }

        return path;
    }

    static string Materialise() {
        var bytes = Bytes();
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes))[..16];
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "skala-oracle-config", digest);
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, ".editorconfig");

        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) {
            return path;
        }

        // ⚠ Written beside and moved over, so a concurrent reader never sees a half-written file.
        var staging = path + "." + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(staging, bytes);
        try {
            File.Move(staging, path, true);
        } catch (IOException) when (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) {
            // Another process wrote the same bytes first, which is the only content this path can hold.
            File.Delete(staging);
        }

        return path;
    }
}
