using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5042</c> — a file or directory created, or <c>chmod</c>ed, writable by every local user.
/// </summary>
/// <remarks>
///     Issue #145 arrived as three SonarQube rules and <b>two of the three are refuted by measurement</b>;
///     this is the third, narrowed to the one bit that needs no judgement.
///     <para>
///         ⚠ <b>"A temporary file is created in a publicly writable directory" is not decidable from the
///         source, because the directory is not publicly writable on two of the three platforms.</b>
///         Measured: <c>Path.GetTempPath()</c> returns a <em>per-user</em> directory on macOS
///         (<c>/var/folders/…/T/</c>, mode <c>0700</c>) and on Windows (<c>%LOCALAPPDATA%\Temp</c>). Only
///         on Linux with <c>TMPDIR</c> unset is it <c>/tmp</c>. The same source text is a vulnerability or
///         not depending on the machine it runs on, which makes it a property of the deployment rather
///         than of the code.
///     </para>
///     <para>
///         ⚠ <b>"<c>Path.GetTempFileName</c> is an insecure temporary file creation method" is refuted on
///         .NET.</b> Measured: it creates the file at mode <c>0600</c> — .NET goes through <c>mkstemp</c>,
///         so the file exists, owned and private, before the name is returned, and the create-then-open
///         race the rule is written about does not arise. What is left of it is a name-exhaustion limit at
///         65 535 files on Windows, which is a robustness bug and not a vulnerability.
///     </para>
///     <para>
///         ⚠ <b>And world-<em>readable</em> is not a rule either, for a measured reason.</b> Plain
///         <c>File.WriteAllText</c> creates at <c>0644</c> — every ordinary file .NET writes is already
///         world-readable, because that is what the process umask says. A rule reporting world-readable
///         files would report every file-writing call in existence. So this rule is about
///         <c>OtherWrite</c> alone: the bit that lets an unrelated local user <em>replace the contents</em>
///         of a file this program will later read and trust.
///     </para>
///     <para>
///         ⚠ <b><c>CA1416</c> is not this diagnostic and does not host it.</b> All three mode-setting APIs
///         are <c>[UnsupportedOSPlatform("windows")]</c>, so <c>CA1416</c> — on by default at
///         <c>warning</c> — does fire at an unscoped call site. But it fires on the <em>platform</em>, not
///         the permission: it says exactly the same thing about <c>UnixFileMode.UserRead</c>, which is
///         safe. The two are independent, and measurably so — scoping the call site with
///         <c>[SupportedOSPlatform("linux")]</c> or an <c>OperatingSystem.IsLinux()</c> guard silences
///         <c>CA1416</c> completely and leaves <c>OtherWrite</c> reported by <b>nothing at all</b>, even
///         under <c>AnalysisMode=All</c> with every <c>CA</c> raised. That residue — platform-scoped Unix
///         code, which is the code that legitimately uses these APIs — is this rule's whole population.
///     </para>
///     <para>
///         ⚠ <b>The sticky bit is the escape and it is the only one.</b> <c>OtherWrite</c> together with
///         <c>StickyBit</c> is the shared drop-box idiom — mode <c>1777</c>, which is what <c>/tmp</c>
///         itself is — where anyone may create an entry and only the owner may remove theirs. That is a
///         deliberate design and the rule declines it. <c>OtherWrite</c> without it is a file or directory
///         any local process can overwrite.
///     </para>
///     <para>
///         ⚠ <b>No receiver is named.</b> The rule matches on the <em>type</em> of the value —
///         <c>System.IO.UnixFileMode</c> — wherever it is passed or assigned. Enumerated from the BCL
///         by reflection, that is today exactly four entry points:
///         <c>File.SetUnixFileMode(string, …)</c>, <c>File.SetUnixFileMode(SafeFileHandle, …)</c>,
///         <c>Directory.CreateDirectory(string, UnixFileMode)</c> and the
///         <c>FileStreamOptions.UnixCreateMode</c> property, which is a <c>UnixFileMode?</c>. None of
///         them is written down here, so a fifth arrives covered — the argument <c>SK5007</c> makes for
///         certificate callbacks. ⚠ <c>File.OpenHandle</c> and <c>File.Open</c> take no mode and are not
///         in this set; an earlier draft of this remark said they were, and the reflection listing is
///         what corrected it.
///     </para>
///     <para>
///         ⚠ <c>hasFix: false</c>. Dropping <c>OtherWrite</c> is a one-token edit, but whether the
///         remaining bits are the permissions this program actually needs is a question about who else
///         reads the file — a deployment fact the compiler cannot see. Removing a bit that something
///         downstream depends on breaks it at run time and not at build time, which is the worst kind of
///         edit for a tool to apply unreviewed.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorldWritableFileModeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WorldWritableFileMode);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var mode = start.Compilation.GetTypeByMetadataName("System.IO.UnixFileMode");
                if (mode is null) {
                    return;
                }

                // ⚠ The two bit values are read off the enum's own members rather than written here as
                // 0x2 and 0x200. A literal would be a second, silent copy of the platform's numbering,
                // and the one place it could go wrong is the place nothing would ever check.
                if (Member(mode, "OtherWrite") is not { } otherWrite || Member(mode, "StickyBit") is not { } sticky) {
                    return;
                }

                var known = new Known(mode, otherWrite, sticky);

                start.RegisterOperationAction(context => Assignment(context, known), OperationKind.SimpleAssignment);
                start.RegisterOperationAction(context => Arguments(context, known), OperationKind.Invocation);
                start.RegisterOperationAction(context => Arguments(context, known), OperationKind.ObjectCreation);
            }
        );
    }

    static long? Member(INamedTypeSymbol enumType, string name) {
        foreach (var candidate in enumType.GetMembers(name)) {
            if (candidate is IFieldSymbol { HasConstantValue: true } field) {
                return System.Convert.ToInt64(field.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    /// <summary><c>options.UnixCreateMode = …</c>, including inside an object initialiser.</summary>
    static void Assignment(OperationAnalysisContext context, Known known) {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (!IsMode(assignment.Target.Type, known.Mode)) {
            return;
        }

        Report(context, assignment.Value, known);
    }

    /// <summary>Any argument whose parameter is a <c>UnixFileMode</c>.</summary>
    static void Arguments(OperationAnalysisContext context, Known known) {
        var arguments = context.Operation switch {
            IInvocationOperation invocation => invocation.Arguments,
            IObjectCreationOperation creation => creation.Arguments,
            _ => ImmutableArray<IArgumentOperation>.Empty
        };

        foreach (var argument in arguments) {
            if (IsMode(argument.Parameter?.Type, known.Mode)) {
                Report(context, argument.Value, known);
            }
        }
    }

    /// <summary><c>UnixFileMode</c> or <c>UnixFileMode?</c> — <c>File.OpenHandle</c> takes the nullable.</summary>
    static bool IsMode(ITypeSymbol? type, INamedTypeSymbol mode) {
        var unwrapped = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } n
            ? n.TypeArguments[0]
            : type;

        return SymbolEqualityComparer.Default.Equals(unwrapped, mode);
    }

    /// <remarks>
    ///     ⚠ Decided on <see cref="IOperation.ConstantValue" />, which is the fact the compiler has
    ///     already computed — so <c>UserRead | UserWrite | OtherWrite</c>, a <c>const</c> holding the
    ///     same, and any parenthesised or cast spelling of it are all one case rather than a list of
    ///     syntax shapes to enumerate. A mode assembled at run time out of variables has no constant
    ///     value and is silence: whether it ends up world-writable is a question about another method.
    /// </remarks>
    static void Report(OperationAnalysisContext context, IOperation value, Known known) {
        // ⚠ Through the conversion, and a fixture is what found this. `FileStreamOptions.UnixCreateMode`
        // is a `UnixFileMode?`, so assigning a folded flag combination to it wraps the constant in an
        // `IConversionOperation` whose own `ConstantValue` is absent — and the rule was silent on its
        // own positive fixture until this unwrapped, exactly as SK5009 once was.
        var operand = ConstantBytes.Unwrap(value);
        if (!operand.ConstantValue.HasValue
            || operand.ConstantValue.Value is not { } raw
            || AsyncContext.IsTestMethod(value.Syntax)) {
            return;
        }

        long bits;
        try {
            bits = System.Convert.ToInt64(raw, System.Globalization.CultureInfo.InvariantCulture);
        } catch (System.Exception exception) when (exception is System.InvalidCastException
                                                       or System.FormatException
                                                       or System.OverflowException) {
            return;
        }

        if ((bits & known.OtherWrite) == 0 || (bits & known.StickyBit) != 0) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                value.Syntax.GetLocation(),
                "this mode sets `UnixFileMode.OtherWrite`, so every local user on the machine may "
                + "replace the contents of what this call creates — and a program that later reads it "
                + "back is trusting whatever they put there; drop `OtherWrite`, or add "
                + "`UnixFileMode.StickyBit` if this really is a shared drop directory where only the "
                + "owner of an entry may remove it"
            )
        );
    }

    readonly struct Known {
        public Known(INamedTypeSymbol mode, long otherWrite, long stickyBit) {
            Mode = mode;
            OtherWrite = otherWrite;
            StickyBit = stickyBit;
        }

        public INamedTypeSymbol Mode { get; }

        public long OtherWrite { get; }

        public long StickyBit { get; }
    }
}
