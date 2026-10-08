using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #528: an <c>=</c> whose value is a single call on a receiver (Newtonsoft's
///     <c>JsonConvert.DeserializeObject&lt;T&gt;(json)</c>) breaks by the measured table in
///     <c>Fitter.HeldValueBreaks</c>, and otherwise the call's own dot breaks. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-09 with <c>Testing ask</c>;
///     <c>constructs/breaks/held-single-call.cs</c> holds the wider set.
/// </summary>
public sealed class HeldSingleCallIssue528Tests {
    const string Long8 = "ValuesAndNamesPerEnum.Get(new StructMultiKey<Type, NamingStrategy?>(enumType, na"
        + "mingStrategy));";

    const string Long9 = "JsonTypeReflector.ReflectionDelegateFactory.CreateDefaultConstructor<object>(tem"
        + "poraryListType);";

    const string Long1 = "PublicParameterizedConstructorRequiringConverterWithParameterAttributeTestClass "
        + "c = JsonConvert.DeserializeObject<PublicParameterizedConstructorRequiringConvert"
        + "erWithParameterAttributeTestClass>(json);";

    const string Long2 = "EnumInfo enumInfo = ValuesAndNamesPerEnum.Get(new StructMultiKey<Type, NamingStr"
        + "ategy?>(enumType, namingStrategy));";

    const string Long3 = "var bottom = device.CreateAccelerationStructure(new(AccelerationStructureKind.Bo"
        + "ttomLevel, bottomSizes.Structure, \"as-bottom\"));";

    const string Long4 = "var methods = CallableConfigurationMethodFinder.FindConfigurationMethods(configu"
        + "rationAssemblies, receiverGroup.Key);";

    const string Long5 = "_genericTemporaryCollectionCreator = JsonTypeReflector.ReflectionDelegateFactory"
        + ".CreateDefaultConstructor<object>(temporaryListType);";

    const string Long6 = ".DeserializeObject<PublicParameterizedConstructorRequiringConverterWithParameter"
        + "AttributeTestClass>(";

    const string Long7 = "CallableConfigurationMethodFinder.FindConfigurationMethods(configurationAssembli"
        + "es, receiverGroup.Key);";

    /// <summary>
    ///     A typed local keeps its <c>=</c> and breaks before the call. Behind <c>var y</c> the same call breaks
    ///     the <c>=</c> while it fits below, breaks the <c>=</c> and the dot when it overflows below by one
    ///     column, chops its argument while its <c>(</c> lands three short of the margin below, and past that
    ///     keeps the <c>=</c> and breaks the dot. Before the fix every row chopped <c>(json)</c> beside the
    ///     <c>=</c>.
    /// </summary>
    [Fact]
    public void ASingleCallValue_FollowsTheMeasuredTable() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      Taaaa c = JsonConvert.DeserializeObject<G{{R('g', 74)}}>(json);
                      var y = Jjjjjjjjjjj.DeserializeObject<G{{R('g', 69)}}>(json);
                      var y = Jjjjjjjjjjj.DeserializeObject<G{{R('g', 70)}}>(json);
                      var y = Jjjjjjjjjjj.DeserializeObject<G{{R('g', 71)}}>(json);
                      var y = Jjjjjjjjjjj.DeserializeObject<G{{R('g', 73)}}>(json);
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      Taaaa c = JsonConvert
                          .DeserializeObject<G{{R('g', 74)}}>(json);
                      var y =
                          Jjjjjjjjjjj.DeserializeObject<G{{R('g', 69)}}>(json);
                      var y =
                          Jjjjjjjjjjj
                              .DeserializeObject<G{{R('g', 70)}}>(json);
                      var y =
                          Jjjjjjjjjjj.DeserializeObject<G{{R('g', 71)}}>(
                              json
                          );
                      var y = Jjjjjjjjjjj
                          .DeserializeObject<G{{R('g', 73)}}>(json);
                  }
              }
              """
        );

    /// <summary>
    ///     The table where the reference trees bound it, each row from its tree and re-measured here at
    ///     indent 8: a typed local whose head and receiver are wider than 87 columns breaks the <c>=</c>
    ///     (Newtonsoft); a typed local's creation argument follows the table; a <c>var</c>'s does not and
    ///     chops (Vixen); behind a long head a value that fits below with three columns to spare moves down
    ///     whole, two arguments included (Serilog, Newtonsoft). Before the fix the first, second and last
    ///     rows came out otherwise.
    /// </summary>
    [Fact]
    public void TheReferenceTreesRows_FollowTheirOracle() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      {{Long1}}
                      {{Long2}}
                      {{Long3}}
                      {{Long4}}
                      {{Long5}}
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      PublicParameterizedConstructorRequiringConverterWithParameterAttributeTestClass c =
                          JsonConvert
                              {{Long6}}
                                  json
                              );
                      EnumInfo enumInfo =
                          {{Long8}}
                      var bottom = device.CreateAccelerationStructure(
                          new(AccelerationStructureKind.BottomLevel, bottomSizes.Structure, "as-bottom")
                      );
                      var methods =
                          {{Long7}}
                      _genericTemporaryCollectionCreator =
                          {{Long9}}
                  }
              }
              """
        );
}
