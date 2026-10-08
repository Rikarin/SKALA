using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>Issue #577's oracle answer, kept apart from the source it answers.</summary>
public sealed partial class ConditionalMovesDownWholeIssue577Tests {
    static readonly string Oracle = $$"""
                                      class T {
                                          object M() {
                                              var vvvvvvvvvvvv = ffff ? {{R('a', 41)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 42)}};
                                              var vvvvvvvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 45)}};
                                              var vvvvvvvvvvvv = fffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvvv =
                                                  fffff ? {{R('a', 41)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv =
                                                  fffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv = ffffff ? {{R('a', 40)}} : {{R('b', 40)}};
                                              var vvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 41)}};
                                              var vvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv = ffffffff ? {{R('a', 39)}} : {{R('b', 39)}};
                                              var vvvvvvvvvvvv =
                                                  ffffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 40)}};
                                              var vvvvvvvvvvvv =
                                                  ffffffff ? {{R('a', 42)}} : {{R('b', 42)}};
                                              var vvvvvvvvvvvv = ffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv = ffffffffff ? {{R('a', 38)}} : {{R('b', 38)}};
                                              var vvvvvvvvvvvv =
                                                  ffffffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 39)}};
                                              var vvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv = fffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvv =
                                                  fffffffffffff ? {{R('a', 37)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvv =
                                                  fffffffffffff ? {{R('a', 38)}} : {{R('b', 39)}};
                                              var vvvvvvvvvvvv = fffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv = ffffffffffffffff ? {{R('a', 35)}} : {{R('b', 35)}};
                                              var vvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 35)}} : {{R('b', 36)}};
                                              var vvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvv = ffffffffffffffffffff ? {{R('a', 33)}} : {{R('b', 33)}};
                                              var vvvvvvvvvvvv =
                                                  ffffffffffffffffffff ? {{R('a', 33)}} : {{R('b', 34)}};
                                              var vvvvvvvvvvvv = {{R('f', 24)}} ? {{R('a', 31)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = ffff ? {{R('a', 38)}} : {{R('b', 39)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffff ? {{R('a', 39)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = fffff ? {{R('a', 38)}} : {{R('b', 38)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 39)}};
                                              var vvvvvvvvvvvvvvvvv = ffffff ? {{R('a', 37)}} : {{R('b', 38)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffff ? {{R('a', 38)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = ffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffff ? {{R('a', 37)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffff ? {{R('a', 42)}} : {{R('b', 42)}};
                                              var vvvvvvvvvvvvvvvvv = ffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = ffffffffff ? {{R('a', 35)}} : {{R('b', 36)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 36)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = fffffffffffff ? {{R('a', 34)}} : {{R('b', 34)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  fffffffffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 35)}};
                                              var vvvvvvvvvvvvvvvvv = fffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 17)}} = ffffffffffffffff ? {{R('a', 32)}} : {{R('b', 33)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 33)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = {{R('f', 20)}} ? {{R('a', 30)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffff ? {{R('a', 31)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffff ? {{R('a', 34)}} : {{R('b', 34)}};
                                              var vvvvvvvvvvvvvvvvv = ffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvv = {{R('f', 24)}} ? {{R('a', 28)}} : {{R('b', 29)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 29)}} : {{R('b', 29)}};
                                              var vvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 31)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 45)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffff ? {{R('a', 42)}} : {{R('b', 42)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffffffffffff ? {{R('a', 38)}} : {{R('b', 39)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = fffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffff ? {{R('a', 33)}} : {{R('b', 34)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 31)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffff ? {{R('a', 43)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffff ? {{R('a', 42)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 42)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffff ? {{R('a', 40)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 39)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffffffffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 37)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = fffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 34)}} : {{R('b', 35)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffff ? {{R('a', 32)}} : {{R('b', 32)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 29)}} : {{R('b', 29)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffff ? {{R('a', 41)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 41)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? {{R('a', 40)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 39)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 37)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffffffffffff ? {{R('a', 35)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 56)}} = fffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 33)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 56)}} = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffff ? {{R('a', 30)}} : {{R('b', 30)}};
                                              var {{R('v', 56)}} = ffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 27)}} : {{R('b', 28)}};
                                              var {{R('v', 56)}} = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvv = ffff ? {{R('a', 45)}} : {{R('b', 46)}};
                                              var vvv = ffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvv = fffff ? {{R('a', 45)}} : {{R('b', 45)}};
                                              var vvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvv = ffffff ? {{R('a', 44)}} : {{R('b', 45)}};
                                              var vvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvv = ffffffffff ? {{R('a', 42)}} : {{R('b', 43)}};
                                              var vvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvv = ffffffffffffffff ? {{R('a', 39)}} : {{R('b', 40)}};
                                              var vvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvv = ffff ? {{R('a', 43)}} : {{R('b', 44)}};
                                              var vvvvvvv =
                                                  ffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 45)}};
                                              var vvvvvvv = fffff ? {{R('a', 43)}} : {{R('b', 43)}};
                                              var vvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvv = ffffff ? {{R('a', 42)}} : {{R('b', 43)}};
                                              var vvvvvvv =
                                                  ffffff ? {{R('a', 43)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvv = ffffffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvv = ffffffffffffffff ? {{R('a', 37)}} : {{R('b', 38)}};
                                              var vvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvv = ffff ? {{R('a', 42)}} : {{R('b', 43)}};
                                              var vvvvvvvvv =
                                                  ffff ? {{R('a', 43)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 45)}};
                                              var vvvvvvvvv = fffff ? {{R('a', 42)}} : {{R('b', 42)}};
                                              var vvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 43)}};
                                              var vvvvvvvvv =
                                                  fffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvv = ffffff ? {{R('a', 41)}} : {{R('b', 42)}};
                                              var vvvvvvvvv =
                                                  ffffff ? {{R('a', 42)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvv =
                                                  ffffff ? {{R('a', 43)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvv = ffffffffff ? {{R('a', 39)}} : {{R('b', 40)}};
                                              var vvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 40)}};
                                              var vvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvv = ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv = ffff ? {{R('a', 41)}} : {{R('b', 42)}};
                                              var vvvvvvvvvvv =
                                                  ffff ? {{R('a', 42)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 45)}};
                                              var vvvvvvvvvvv = fffff ? {{R('a', 41)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 42)}};
                                              var vvvvvvvvvvv =
                                                  fffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv = ffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvv =
                                                  ffffff ? {{R('a', 41)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv = ffffffffff ? {{R('a', 38)}} : {{R('b', 39)}};
                                              var vvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 39)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvv = ffffffffffffffff ? {{R('a', 35)}} : {{R('b', 36)}};
                                              var vvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 36)}};
                                              var vvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv = ffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvvvv =
                                                  ffff ? {{R('a', 41)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv = fffff ? {{R('a', 40)}} : {{R('b', 40)}};
                                              var vvvvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 41)}};
                                              var vvvvvvvvvvvvv =
                                                  fffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv = ffffff ? {{R('a', 39)}} : {{R('b', 40)}};
                                              var vvvvvvvvvvvvv =
                                                  ffffff ? {{R('a', 40)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv = ffffffffff ? {{R('a', 37)}} : {{R('b', 38)}};
                                              var vvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 38)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvv = ffffffffffffffff ? {{R('a', 34)}} : {{R('b', 35)}};
                                              var vvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 35)}} : {{R('b', 35)}};
                                              var vvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = ffff ? {{R('a', 39)}} : {{R('b', 40)}};
                                              var vvvvvvvvvvvvvvv =
                                                  ffff ? {{R('a', 40)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = fffff ? {{R('a', 39)}} : {{R('b', 39)}};
                                              var vvvvvvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 40)}};
                                              var vvvvvvvvvvvvvvv =
                                                  fffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = ffffff ? {{R('a', 38)}} : {{R('b', 39)}};
                                              var vvvvvvvvvvvvvvv =
                                                  ffffff ? {{R('a', 39)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = ffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 37)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 41)}};
                                              var vvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = ffffffffffffffff ? {{R('a', 33)}} : {{R('b', 34)}};
                                              var vvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 34)}} : {{R('b', 34)}};
                                              var vvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 37)}};
                                              var vvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 45)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? {{R('a', 43)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 40)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 36)}} : {{R('b', 36)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffff ? {{R('a', 44)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 43)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 40)}} : {{R('b', 40)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 35)}} : {{R('b', 36)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 44)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffff ? {{R('a', 43)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? {{R('a', 42)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 40)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 35)}} : {{R('b', 36)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffff ? {{R('a', 43)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  fffff ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 43)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = fffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffff ? {{R('a', 42)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffff ? {{R('a', 39)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffff ? {{R('a', 35)}} : {{R('b', 35)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = {{R('f', 24)}} ? {{R('a', 29)}} : {{R('b', 30)}};
                                              var vvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 30)}} : {{R('b', 30)}};
                                              var vvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 31)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = {{R('f', 29)}} ? {{R('a', 27)}} : {{R('b', 27)}};
                                              var vvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? aaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 28)}};
                                              var vvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? aaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 29)}};
                                              var vvvvvvvvvvvvvvv = fffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = {{R('f', 34)}} ? {{R('a', 24)}} : {{R('b', 25)}};
                                              var vvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? {{R('a', 25)}} : bbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? {{R('a', 26)}} : bbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = ffffffffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = {{R('f', 40)}} ? {{R('a', 21)}} : {{R('b', 22)}};
                                              var vvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvv = ffffffffffffffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 21)}} = {{R('f', 24)}} ? {{R('a', 26)}} : {{R('b', 27)}};
                                              var vvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 27)}} : {{R('b', 27)}};
                                              var vvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 21)}} = {{R('f', 29)}} ? {{R('a', 24)}} : {{R('b', 24)}};
                                              var vvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? aaaaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvv = fffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 21)}} = {{R('f', 34)}} ? {{R('a', 21)}} : {{R('b', 22)}};
                                              var vvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? aaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 21)}} = {{R('f', 40)}} ? aaaaaaaaaaaaaaaaaa : {{R('b', 19)}};
                                              var vvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 25)}} = {{R('f', 24)}} ? {{R('a', 24)}} : {{R('b', 25)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 25)}} : bbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 31)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 25)}} = {{R('f', 29)}} ? {{R('a', 22)}} : {{R('b', 22)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? aaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? aaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 29)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv = fffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 25)}} = {{R('f', 34)}} ? {{R('a', 19)}} : {{R('b', 20)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? aaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? {{R('a', 26)}} : bbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 25)}} = {{R('f', 40)}} ? aaaaaaaaaaaaaaaa : {{R('b', 17)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 27)}} = {{R('f', 24)}} ? {{R('a', 23)}} : {{R('b', 24)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 24)}} ? aaaaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 31)}} : {{R('b', 31)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 27)}} = {{R('f', 29)}} ? {{R('a', 21)}} : {{R('b', 21)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? aaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? aaaaaaaaaaaaaaaaaaaaaaaaaaaa : {{R('b', 29)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = fffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 27)}} = {{R('f', 34)}} ? aaaaaaaaaaaaaaaaaa : {{R('b', 19)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? aaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? {{R('a', 26)}} : bbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv = ffffffffffffffffffffffffffffffffff
                                                  ? aaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  : bbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var {{R('v', 27)}} = {{R('f', 40)}} ? aaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  ffffffffffffffffffffffff ? {{R('a', 29)}} : {{R('b', 30)}};
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 29)}} ? {{R('a', 27)}} : bbbbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 34)}} ? aaaaaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbbbbb;
                                              var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                  {{R('f', 40)}} ? aaaaaaaaaaaaaaaaaaaaa : bbbbbbbbbbbbbbbbbbbbbb;
                                              return null;
                                          }
                                      }
                                      """;

    [Fact]
    public void EveryBoundaryRow_ComesBackAsTheOracleWritesIt() {
        var formatted = FormatWith(Source);
        Assert.Equal(Oracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
