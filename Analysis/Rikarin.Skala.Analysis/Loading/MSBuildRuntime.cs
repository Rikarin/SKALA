using Microsoft.Build.Locator;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     Makes the SDK's MSBuild assemblies loadable, once per process.
/// </summary>
/// <remarks>
///     ⚠ The binlog path needs this, and that is not obvious. ADR-007 chose the binary log precisely to
///     avoid MSBuild — no evaluation, no design-time build, no SDK-version sensitivity — but
///     <c>MSBuild.StructuredLogger</c> deserialises the log into MSBuild's own event types, so
///     <c>Microsoft.Build.Framework</c> has to be loadable at run time to <em>read</em> one. Skala
///     cannot ship its own copy: <c>MSBuildLocator</c> requires the SDK's to win, and shipping ours is
///     how you get an assembly-load failure that names a file which is obviously present (MSBL001 is
///     that rule, enforced at build time).
///     <para>
///         So the resolution is the locator's, for both load modes. What ADR-007 actually buys is still
///         intact: nothing here evaluates a project, runs a target, or asks MSBuild what the build
///         <em>would</em> do. It only makes the types the log is written in resolvable.
///     </para>
///     <para>
///         ⚠ Every caller must keep MSBuild types out of its own frame until this has run — the locator
///         installs an <c>AssemblyResolve</c> handler, and a method the JIT has already prepared resolves
///         its references before the handler exists. That is why the callers put the MSBuild work behind a
///         <c>MethodImplOptions.NoInlining</c> boundary.
///     </para>
/// </remarks>
public static class MSBuildRuntime {
    static int registered;

    /// <summary>Whether the SDK's MSBuild was found. False is reported, never thrown.</summary>
    public static bool Ensure(out string? error) {
        error = null;
        if (Interlocked.CompareExchange(ref registered, 1, 0) != 0) {
            return true;
        }

        try {
            if (!MSBuildLocator.IsRegistered) {
                MSBuildLocator.RegisterDefaults();
            }

            return true;
        } catch (InvalidOperationException exception) {
            error = exception.Message;
            return false;
        }
    }
}
