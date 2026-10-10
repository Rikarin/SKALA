using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <remarks>
///     Round 3 (2026-10-10): rows drawn from fresh probes for the round-three rules, each the oracle's own answer.
/// </remarks>
public sealed partial class EqualsByTheNameTests {
    /// <summary>Round 3: a field behind <c>[A] /* c */</c> declines the join up to <c>EqualsFloor.DeclinesTheJoin</c>.</summary>
    static readonly string RoundThreeCommentedAttributesSource = $$"""
                                                                   class C {
                                                                       [JsonProperty] /* {{R('c', 12)}} */ public List<T> N{{R('a', 15)}} = alpha.Beta.{{R('Z', 75)}};

                                                                       [A] /* ccccc */ private static string N{{R('a', 21)}} = alpha + beta + {{R('z', 60)}};

                                                                       [Obsolete] /* {{R('c', 12)}} */ internal readonly T{{R('y', 20)}} Naaaaaaaa = flag ? alpha : {{R('z', 41)}};

                                                                       [JsonProperty] /* {{R('c', 12)}} */ internal readonly T{{R('y', 20)}} N{{R('a', 18)}} = flag ? alpha : {{R('z', 57)}};

                                                                       [DataMember(Order = 1)] /* cccccc */ public string N{{R('a', 21)}} = "{{R('s', 86)}}";

                                                                       [NonSerialized] /**/ public int N{{R('a', 14)}} = "{{R('s', 108)}}";

                                                                       [NonSerialized] /* {{R('c', 13)}} */ private string Naaaaaa = alpha + beta + {{R('z', 90)}};

                                                                       [NonSerialized] /* cccc */ IReadOnlyList<KeyValuePair<string, object>> N{{R('a', 19)}} = "{{R('s', 68)}}";

                                                                       [JsonProperty] /* cc */ internal readonly int Naaaaaaaa = "{{R('s', 72)}}";

                                                                       [A] /**/ internal readonly string N{{R('a', 18)}} = "{{R('s', 108)}}";

                                                                       [Obsolete] /**/ public List<T> N{{R('a', 28)}} = "{{R('s', 62)}}";

                                                                       [DataMember(Order = 1)] /* cccccccccc */ public Dictionary<string, int> N{{R('a', 21)}} = {{R('z', 32)}};

                                                                       [DataMember(Order = 1)] /**/ public IReadOnlyList<KeyValuePair<string, object>> N{{R('a', 15)}} = alpha.Beta.ZZZZZZZZZ;

                                                                       [Obsolete] /**/ protected static readonly List<T> N{{R('a', 17)}} = flag ? alpha : {{R('z', 71)}};

                                                                       [Obsolete] /* cccc */ private string N{{R('a', 27)}} = "{{R('s', 72)}}";

                                                                       [NonSerialized] /**/ string Naaaaaa = "{{R('s', 118)}}";
                                                                   }
                                                                   """;

    /// <summary>Round 3: a typed local before a single call on a receiver: <c>EqualsFloor.HeldTypedLocalBreaks</c>.</summary>
    static readonly string RoundThreeHeldTypedLocalsSource = $$"""
                                                               class C {
                                                                   void M0() {
                                                                       List<T> daaaaaaa = x.GetBytes("{{R('s', 83)}}");
                                                                   }
                                                                   void M1() {
                                                                       byte[] daaaaaaaaaaa = Convert.FromBase64String({{R('v', 78)}});
                                                                   }
                                                                   void M2() {
                                                                       T{{R('y', 15)}} daaaa = x.DeserializeObject<T>("{{R('s', 77)}}");
                                                                   }
                                                                   void M3() {
                                                                       string d = Encoding.UTF8.CreateInstanceOfTheThing("{{R('s', 62)}}");
                                                                   }
                                                                   void M4() {
                                                                       int d{{R('a', 15)}} = x.Get("{{R('s', 83)}}");
                                                                   }
                                                                   void M5() {
                                                                       byte[] d{{R('a', 18)}} = x.FromBase64String(alpha, {{R('b', 84)}});
                                                                   }
                                                                   void M6() {
                                                                       string daaaaaaaaa = JsonConvert.CreateInstanceOfTheThing({{R('v', 79)}});
                                                                   }
                                                                   void M7() {
                                                                       Dictionary<string, int> daaa = Encoding.UTF8.GetBytes({{R('v', 58)}});
                                                                   }
                                                                   void M8() {
                                                                       T{{R('y', 15)}} daaaa = JsonConvert.FromBase64String("{{R('s', 58)}}");
                                                                   }
                                                                   void M9() {
                                                                       IReadOnlyList<KeyValuePair<string, object>> daaaaaa = x.FromBase64String({{R('v', 44)}});
                                                                   }
                                                                   void M10() {
                                                                       T{{R('y', 15)}} d{{R('a', 12)}} = x.FromBase64String("{{R('s', 73)}}");
                                                                   }
                                                                   void M11() {
                                                                       T{{R('y', 15)}} daaaa = _serializer.Get({{R('v', 81)}});
                                                                   }
                                                                   void M12() {
                                                                       int daaaaaaaa = Convert.FromBase64String(alpha, {{R('b', 82)}});
                                                                   }
                                                                   void M13() {
                                                                       string daaaaaaaaa = Convert.Parse("{{R('s', 78)}}");
                                                                   }
                                                                   void M14() {
                                                                       byte[] d{{R('a', 18)}} = this.Repository.Parse({{R('v', 56)}});
                                                                   }
                                                               }
                                                               """;

    static readonly string RoundThreeCommentedAttributesOracle = $$"""
                                                                   class C {
                                                                       [JsonProperty] /* {{R('c', 12)}} */ public List<T> N{{R('a', 15)}} =
                                                                           alpha.Beta.{{R('Z', 75)}};

                                                                       [A] /* ccccc */ private static string N{{R('a', 21)}} =
                                                                           alpha + beta + {{R('z', 60)}};

                                                                       [Obsolete] /* {{R('c', 12)}} */
                                                                       internal readonly T{{R('y', 20)}} Naaaaaaaa = flag ? alpha : {{R('z', 41)}};

                                                                       [JsonProperty] /* {{R('c', 12)}} */ internal readonly T{{R('y', 20)}} N{{R('a', 18)}} =
                                                                           flag ? alpha : {{R('z', 57)}};

                                                                       [DataMember(Order = 1)] /* cccccc */ public string N{{R('a', 21)}} =
                                                                           "{{R('s', 86)}}";

                                                                       [NonSerialized] /**/ public int N{{R('a', 14)}} =
                                                                           "{{R('s', 108)}}";

                                                                       [NonSerialized] /* {{R('c', 13)}} */ private string Naaaaaa = alpha
                                                                           + beta
                                                                           + {{R('z', 90)}};

                                                                       [NonSerialized] /* cccc */ IReadOnlyList<KeyValuePair<string, object>> N{{R('a', 19)}} =
                                                                           "{{R('s', 68)}}";

                                                                       [JsonProperty] /* cc */
                                                                       internal readonly int Naaaaaaaa = "{{R('s', 72)}}";

                                                                       [A] /**/ internal readonly string N{{R('a', 18)}} =
                                                                           "{{R('s', 108)}}";

                                                                       [Obsolete] /**/ public List<T> N{{R('a', 28)}} =
                                                                           "{{R('s', 62)}}";

                                                                       [DataMember(Order = 1)] /* cccccccccc */
                                                                       public Dictionary<string, int> N{{R('a', 21)}} = {{R('z', 32)}};

                                                                       [DataMember(Order = 1)] /**/
                                                                       public IReadOnlyList<KeyValuePair<string, object>> N{{R('a', 15)}} = alpha.Beta.ZZZZZZZZZ;

                                                                       [Obsolete] /**/ protected static readonly List<T> N{{R('a', 17)}} =
                                                                           flag ? alpha : {{R('z', 71)}};

                                                                       [Obsolete] /* cccc */ private string N{{R('a', 27)}} =
                                                                           "{{R('s', 72)}}";

                                                                       [NonSerialized] /**/ string Naaaaaa =
                                                                           "{{R('s', 118)}}";
                                                                   }
                                                                   """;

    static readonly string RoundThreeHeldTypedLocalsOracle = $$"""
                                                               class C {
                                                                   void M0() {
                                                                       List<T> daaaaaaa = x.GetBytes(
                                                                           "{{R('s', 83)}}"
                                                                       );
                                                                   }

                                                                   void M1() {
                                                                       byte[] daaaaaaaaaaa = Convert.FromBase64String(
                                                                           {{R('v', 78)}}
                                                                       );
                                                                   }

                                                                   void M2() {
                                                                       T{{R('y', 15)}} daaaa = x.DeserializeObject<T>(
                                                                           "{{R('s', 77)}}"
                                                                       );
                                                                   }

                                                                   void M3() {
                                                                       string d = Encoding.UTF8.CreateInstanceOfTheThing(
                                                                           "{{R('s', 62)}}"
                                                                       );
                                                                   }

                                                                   void M4() {
                                                                       int d{{R('a', 15)}} = x.Get(
                                                                           "{{R('s', 83)}}"
                                                                       );
                                                                   }

                                                                   void M5() {
                                                                       byte[] d{{R('a', 18)}} = x.FromBase64String(
                                                                           alpha,
                                                                           {{R('b', 84)}}
                                                                       );
                                                                   }

                                                                   void M6() {
                                                                       string daaaaaaaaa = JsonConvert.CreateInstanceOfTheThing(
                                                                           {{R('v', 79)}}
                                                                       );
                                                                   }

                                                                   void M7() {
                                                                       Dictionary<string, int> daaa =
                                                                           Encoding.UTF8.GetBytes({{R('v', 58)}});
                                                                   }

                                                                   void M8() {
                                                                       T{{R('y', 15)}} daaaa =
                                                                           JsonConvert.FromBase64String("{{R('s', 58)}}");
                                                                   }

                                                                   void M9() {
                                                                       IReadOnlyList<KeyValuePair<string, object>> daaaaaa =
                                                                           x.FromBase64String({{R('v', 44)}});
                                                                   }

                                                                   void M10() {
                                                                       T{{R('y', 15)}} d{{R('a', 12)}} =
                                                                           x.FromBase64String("{{R('s', 73)}}");
                                                                   }

                                                                   void M11() {
                                                                       T{{R('y', 15)}} daaaa = _serializer.Get(
                                                                           {{R('v', 81)}}
                                                                       );
                                                                   }

                                                                   void M12() {
                                                                       int daaaaaaaa = Convert.FromBase64String(
                                                                           alpha,
                                                                           {{R('b', 82)}}
                                                                       );
                                                                   }

                                                                   void M13() {
                                                                       string daaaaaaaaa =
                                                                           Convert.Parse("{{R('s', 78)}}");
                                                                   }

                                                                   void M14() {
                                                                       byte[] d{{R('a', 18)}} = this.Repository.Parse({{R('v', 56)}});
                                                                   }
                                                               }
                                                               """;

    [Fact]
    public void RoundThreeCommentedAttributes_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(RoundThreeCommentedAttributesSource);
        Assert.Equal(RoundThreeCommentedAttributesOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    [Fact]
    public void RoundThreeHeldTypedLocals_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(RoundThreeHeldTypedLocalsSource);
        Assert.Equal(RoundThreeHeldTypedLocalsOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
