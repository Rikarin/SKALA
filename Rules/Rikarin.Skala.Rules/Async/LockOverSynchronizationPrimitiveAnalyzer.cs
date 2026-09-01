using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3040</c> — a <c>lock</c> statement is taken over a synchronization primitive.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     <c>lock (semaphore)</c> compiles because every reference type carries a monitor, so the author
///     gets a monitor they did not mean, taken over an object whose own waiting happens somewhere
///     else entirely. The two mechanisms do not compose: a thread parked inside
///     <c>semaphore.Wait()</c> holds nothing the monitor knows about, and a thread inside
///     <c>lock (semaphore)</c> excludes nobody who calls <c>Wait</c>. Where the primitive takes its
///     own monitor internally, the mistake is a deadlock rather than a no-op.
///     <para>
///         ⚠ This is not <c>SK1023</c>. That rule looks at a private <c>readonly object</c> field whose
///         only use is as a lock target and offers <c>System.Threading.Lock</c> instead — it is a
///         modernization of a lock that is already correct. This one is about locking on an object that
///         is <em>already</em> a lock of a different kind, and it never fires on <c>object</c>. The two
///         are disjoint by the type of the lock target.
///     </para>
///     <para>
///         ⚠ <c>System.Threading.Lock</c> is excluded — see <see cref="PrimitiveNames" /> for how, and
///         for why the exclusion is an absence from a list rather than a guard in the walk.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LockOverSynchronizationPrimitiveAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     ⚠ Resolved from the compilation, never matched on the written name. <c>Semaphore</c> and
    ///     <c>Barrier</c> are plausible names for somebody's own type, and a finding on one of those
    ///     would send a reader to threading code that is fine.
    /// </summary>
    /// <remarks>
    ///     Everything below <c>WaitHandle</c> — <c>Mutex</c>, <c>Semaphore</c>, <c>EventWaitHandle</c>
    ///     and therefore <c>ManualResetEvent</c> and <c>AutoResetEvent</c> — is reached by the
    ///     base-type walk rather than named here.
    ///     <para>
    ///         ⚠ <c>System.Threading.Lock</c> is a synchronization primitive and is deliberately
    ///         <em>absent</em> from this list, because it is also the one type a C# 13 <c>lock</c>
    ///         statement is supposed to be taken over: the compiler lowers that to
    ///         <c>Lock.EnterScope</c> rather than to <c>Monitor</c>, and reporting it would contradict
    ///         <c>SK1023</c>'s own fix. The absence is load-bearing and
    ///         <c>SK3040/negative/the-dedicated-lock.cs</c> is what holds it: adding the name here
    ///         turns that fixture red. An earlier draft wrote the exclusion as a guard inside
    ///         <c>Analyze</c> instead, which was dead code — <c>Lock</c> derives from <c>object</c>, so
    ///         the walk never reached it and no sabotage could kill the branch.
    ///     </para>
    /// </remarks>
    static readonly string[] PrimitiveNames = {
        "System.Threading.WaitHandle", "System.Threading.SemaphoreSlim", "System.Threading.ManualResetEventSlim",
        "System.Threading.ReaderWriterLockSlim", "System.Threading.ReaderWriterLock", "System.Threading.CountdownEvent",
        "System.Threading.Barrier"
    };

    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.LockOverSynchronizationPrimitive);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var primitives = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var name in PrimitiveNames) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        primitives.Add(type);
                    }
                }

                if (primitives.Count == 0) {
                    return;
                }

                var resolved = primitives.ToImmutable();
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, resolved),
                    SyntaxKind.LockStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> primitives) {
        var statement = (LockStatementSyntax)context.Node;
        if (context.SemanticModel.GetTypeInfo(statement.Expression, context.CancellationToken).Type
            is not INamedTypeSymbol { TypeKind: TypeKind.Class } type) {
            return;
        }

        if (!IsPrimitive(type, primitives)) {
            return;
        }

        // ⚠ The declared type is what the message names, not the base that matched: "lock is taken
        // over ManualResetEvent" reads as the code does, where "over WaitHandle" sends the reader
        // looking for a type the file never mentions.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.Expression.GetLocation(),
                "`lock` is taken over `"
                + type.Name
                + "`, which is itself a synchronization primitive; a monitor and the primitive's own waiting do not exclude each other"
            )
        );
    }

    static bool IsPrimitive(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> primitives) {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType) {
            var definition = current.OriginalDefinition;
            foreach (var primitive in primitives) {
                if (SymbolEqualityComparer.Default.Equals(definition, primitive)) {
                    return true;
                }
            }
        }

        return false;
    }
}
