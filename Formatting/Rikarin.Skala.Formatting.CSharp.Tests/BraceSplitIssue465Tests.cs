using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     <c>csharp_new_line_before_open_brace</c>'s split direction (issue #465, SK-DIV-0091): a brace the
///     key puts on a line of its own goes there from a K&amp;R input too.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration, one value of the key per run. Brace placement used to be a
///     join decision only, so a K&amp;R input came back K&amp;R at every value; the sweep's fixture for the
///     key is written Allman and could not see it.
/// </remarks>
public sealed partial class BraceSplitIssue465Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string KAndR = """
                         using System;
                         using System.Collections.Generic;
                         namespace N {
                             class C {
                                 int _n;
                                 int A {
                                     get {
                                         _n++;
                                         return _n;
                                     }
                                 }
                                 int B { get; set; }
                                 int D { get { return _n; } }
                                 event Action E {
                                     add {
                                         _n++;
                                         _n++;
                                     }
                                     remove { }
                                 }
                                 void M(bool c) {
                                     if (c) {
                                         M(c);
                                         M(!c);
                                     } else {
                                         M(!c);
                                     }
                                     try {
                                         M(c);
                                     } finally {
                                         M(c);
                                     }
                                     switch (c) {
                                         case true:
                                             break;
                                     }
                                     void Local() {
                                         M(c);
                                         M(!c);
                                     }
                                     Action a = () => {
                                         M(c);
                                         M(!c);
                                     };
                                     Action b = delegate { M(c); };
                                     Register(() => {
                                         M(c);
                                         M(!c);
                                     });
                                     var l = new List<int> {
                                         1,
                                         2,
                                         3,
                                         4,
                                         5
                                     };
                                     var o = new { X = 1 };
                                     var r = 1 switch {
                                         1 => 2,
                                         _ => 3
                                     };
                                 }
                                 void Empty() { }
                                 void Register(Action a) {
                                     a();
                                     a();
                                 }
                             }
                             enum F {
                                 G
                             }
                         }
                         """;

    const string All = """
                       using System;
                       using System.Collections.Generic;

                       namespace N
                       {
                           class C
                           {
                               int _n;

                               int A
                               {
                                   get
                                   {
                                       _n++;
                                       return _n;
                                   }
                               }

                               int B { get; set; }

                               int D
                               {
                                   get { return _n; }
                               }

                               event Action E
                               {
                                   add
                                   {
                                       _n++;
                                       _n++;
                                   }
                                   remove { }
                               }

                               void M(bool c)
                               {
                                   if (c)
                                   {
                                       M(c);
                                       M(!c);
                                   } else
                                   {
                                       M(!c);
                                   }

                                   try
                                   {
                                       M(c);
                                   } finally
                                   {
                                       M(c);
                                   }

                                   switch (c)
                                   {
                                       case true:
                                           break;
                                   }

                                   void Local()
                                   {
                                       M(c);
                                       M(!c);
                                   }

                                   Action a = () =>
                                   {
                                       M(c);
                                       M(!c);
                                   };
                                   Action b = delegate { M(c); };
                                   Register(() =>
                                       {
                                           M(c);
                                           M(!c);
                                       }
                                   );
                                   var l = new List<int>
                                   {
                                       1,
                                       2,
                                       3,
                                       4,
                                       5
                                   };
                                   var o = new { X = 1 };
                                   var r = 1 switch
                                   {
                                       1 => 2,
                                       _ => 3
                                   };
                               }

                               void Empty()
                               { }

                               void Register(Action a)
                               {
                                   a();
                                   a();
                               }
                           }

                           enum F
                           {
                               G
                           }
                       }
                       """;

    const string Types = """
                         using System;
                         using System.Collections.Generic;

                         namespace N
                         {
                             class C
                             {
                                 int _n;

                                 int A {
                                     get {
                                         _n++;
                                         return _n;
                                     }
                                 }

                                 int B { get; set; }

                                 int D {
                                     get { return _n; }
                                 }

                                 event Action E {
                                     add {
                                         _n++;
                                         _n++;
                                     }
                                     remove { }
                                 }

                                 void M(bool c) {
                                     if (c) {
                                         M(c);
                                         M(!c);
                                     } else {
                                         M(!c);
                                     }

                                     try {
                                         M(c);
                                     } finally {
                                         M(c);
                                     }

                                     switch (c) {
                                         case true:
                                             break;
                                     }

                                     void Local() {
                                         M(c);
                                         M(!c);
                                     }

                                     Action a = () => {
                                         M(c);
                                         M(!c);
                                     };
                                     Action b = delegate { M(c); };
                                     Register(() => {
                                             M(c);
                                             M(!c);
                                         }
                                     );
                                     var l = new List<int> {
                                         1,
                                         2,
                                         3,
                                         4,
                                         5
                                     };
                                     var o = new { X = 1 };
                                     var r = 1 switch {
                                         1 => 2,
                                         _ => 3
                                     };
                                 }

                                 void Empty() { }

                                 void Register(Action a) {
                                     a();
                                     a();
                                 }
                             }

                             enum F
                             {
                                 G
                             }
                         }
                         """;

    const string Methods = """
                           using System;
                           using System.Collections.Generic;

                           namespace N {
                               class C {
                                   int _n;

                                   int A {
                                       get {
                                           _n++;
                                           return _n;
                                       }
                                   }

                                   int B { get; set; }

                                   int D {
                                       get { return _n; }
                                   }

                                   event Action E {
                                       add {
                                           _n++;
                                           _n++;
                                       }
                                       remove { }
                                   }

                                   void M(bool c)
                                   {
                                       if (c) {
                                           M(c);
                                           M(!c);
                                       } else {
                                           M(!c);
                                       }

                                       try {
                                           M(c);
                                       } finally {
                                           M(c);
                                       }

                                       switch (c) {
                                           case true:
                                               break;
                                       }

                                       void Local()
                                       {
                                           M(c);
                                           M(!c);
                                       }

                                       Action a = () => {
                                           M(c);
                                           M(!c);
                                       };
                                       Action b = delegate { M(c); };
                                       Register(() => {
                                               M(c);
                                               M(!c);
                                           }
                                       );
                                       var l = new List<int> {
                                           1,
                                           2,
                                           3,
                                           4,
                                           5
                                       };
                                       var o = new { X = 1 };
                                       var r = 1 switch {
                                           1 => 2,
                                           _ => 3
                                       };
                                   }

                                   void Empty()
                                   { }

                                   void Register(Action a)
                                   {
                                       a();
                                       a();
                                   }
                               }

                               enum F {
                                   G
                               }
                           }
                           """;

    const string Properties = """
                              using System;
                              using System.Collections.Generic;

                              namespace N {
                                  class C {
                                      int _n;

                                      int A
                                      {
                                          get {
                                              _n++;
                                              return _n;
                                          }
                                      }

                                      int B { get; set; }

                                      int D
                                      {
                                          get { return _n; }
                                      }

                                      event Action E
                                      {
                                          add {
                                              _n++;
                                              _n++;
                                          }
                                          remove { }
                                      }

                                      void M(bool c) {
                                          if (c) {
                                              M(c);
                                              M(!c);
                                          } else {
                                              M(!c);
                                          }

                                          try {
                                              M(c);
                                          } finally {
                                              M(c);
                                          }

                                          switch (c) {
                                              case true:
                                                  break;
                                          }

                                          void Local() {
                                              M(c);
                                              M(!c);
                                          }

                                          Action a = () => {
                                              M(c);
                                              M(!c);
                                          };
                                          Action b = delegate { M(c); };
                                          Register(() => {
                                                  M(c);
                                                  M(!c);
                                              }
                                          );
                                          var l = new List<int> {
                                              1,
                                              2,
                                              3,
                                              4,
                                              5
                                          };
                                          var o = new { X = 1 };
                                          var r = 1 switch {
                                              1 => 2,
                                              _ => 3
                                          };
                                      }

                                      void Empty() { }

                                      void Register(Action a) {
                                          a();
                                          a();
                                      }
                                  }

                                  enum F {
                                      G
                                  }
                              }
                              """;

    const string Accessors = """
                             using System;
                             using System.Collections.Generic;

                             namespace N {
                                 class C {
                                     int _n;

                                     int A {
                                         get
                                         {
                                             _n++;
                                             return _n;
                                         }
                                     }

                                     int B { get; set; }

                                     int D {
                                         get { return _n; }
                                     }

                                     event Action E {
                                         add
                                         {
                                             _n++;
                                             _n++;
                                         }
                                         remove { }
                                     }

                                     void M(bool c) {
                                         if (c) {
                                             M(c);
                                             M(!c);
                                         } else {
                                             M(!c);
                                         }

                                         try {
                                             M(c);
                                         } finally {
                                             M(c);
                                         }

                                         switch (c) {
                                             case true:
                                                 break;
                                         }

                                         void Local() {
                                             M(c);
                                             M(!c);
                                         }

                                         Action a = () => {
                                             M(c);
                                             M(!c);
                                         };
                                         Action b = delegate { M(c); };
                                         Register(() => {
                                                 M(c);
                                                 M(!c);
                                             }
                                         );
                                         var l = new List<int> {
                                             1,
                                             2,
                                             3,
                                             4,
                                             5
                                         };
                                         var o = new { X = 1 };
                                         var r = 1 switch {
                                             1 => 2,
                                             _ => 3
                                         };
                                     }

                                     void Empty() { }

                                     void Register(Action a) {
                                         a();
                                         a();
                                     }
                                 }

                                 enum F {
                                     G
                                 }
                             }
                             """;

    const string ControlBlocks = """
                                 using System;
                                 using System.Collections.Generic;

                                 namespace N {
                                     class C {
                                         int _n;

                                         int A {
                                             get {
                                                 _n++;
                                                 return _n;
                                             }
                                         }

                                         int B { get; set; }

                                         int D {
                                             get { return _n; }
                                         }

                                         event Action E {
                                             add {
                                                 _n++;
                                                 _n++;
                                             }
                                             remove { }
                                         }

                                         void M(bool c) {
                                             if (c)
                                             {
                                                 M(c);
                                                 M(!c);
                                             } else
                                             {
                                                 M(!c);
                                             }

                                             try
                                             {
                                                 M(c);
                                             } finally
                                             {
                                                 M(c);
                                             }

                                             switch (c)
                                             {
                                                 case true:
                                                     break;
                                             }

                                             void Local() {
                                                 M(c);
                                                 M(!c);
                                             }

                                             Action a = () => {
                                                 M(c);
                                                 M(!c);
                                             };
                                             Action b = delegate { M(c); };
                                             Register(() => {
                                                     M(c);
                                                     M(!c);
                                                 }
                                             );
                                             var l = new List<int> {
                                                 1,
                                                 2,
                                                 3,
                                                 4,
                                                 5
                                             };
                                             var o = new { X = 1 };
                                             var r = 1 switch {
                                                 1 => 2,
                                                 _ => 3
                                             };
                                         }

                                         void Empty() { }

                                         void Register(Action a) {
                                             a();
                                             a();
                                         }
                                     }

                                     enum F {
                                         G
                                     }
                                 }
                                 """;

    const string Lambdas = """
                           using System;
                           using System.Collections.Generic;

                           namespace N {
                               class C {
                                   int _n;

                                   int A {
                                       get {
                                           _n++;
                                           return _n;
                                       }
                                   }

                                   int B { get; set; }

                                   int D {
                                       get { return _n; }
                                   }

                                   event Action E {
                                       add {
                                           _n++;
                                           _n++;
                                       }
                                       remove { }
                                   }

                                   void M(bool c) {
                                       if (c) {
                                           M(c);
                                           M(!c);
                                       } else {
                                           M(!c);
                                       }

                                       try {
                                           M(c);
                                       } finally {
                                           M(c);
                                       }

                                       switch (c) {
                                           case true:
                                               break;
                                       }

                                       void Local() {
                                           M(c);
                                           M(!c);
                                       }

                                       Action a = () =>
                                       {
                                           M(c);
                                           M(!c);
                                       };
                                       Action b = delegate { M(c); };
                                       Register(() =>
                                           {
                                               M(c);
                                               M(!c);
                                           }
                                       );
                                       var l = new List<int> {
                                           1,
                                           2,
                                           3,
                                           4,
                                           5
                                       };
                                       var o = new { X = 1 };
                                       var r = 1 switch {
                                           1 => 2,
                                           _ => 3
                                       };
                                   }

                                   void Empty() { }

                                   void Register(Action a) {
                                       a();
                                       a();
                                   }
                               }

                               enum F {
                                   G
                               }
                           }
                           """;

    const string ObjectCollectionArrayInitializers = """
                                                     using System;
                                                     using System.Collections.Generic;

                                                     namespace N {
                                                         class C {
                                                             int _n;

                                                             int A {
                                                                 get {
                                                                     _n++;
                                                                     return _n;
                                                                 }
                                                             }

                                                             int B { get; set; }

                                                             int D {
                                                                 get { return _n; }
                                                             }

                                                             event Action E {
                                                                 add {
                                                                     _n++;
                                                                     _n++;
                                                                 }
                                                                 remove { }
                                                             }

                                                             void M(bool c) {
                                                                 if (c) {
                                                                     M(c);
                                                                     M(!c);
                                                                 } else {
                                                                     M(!c);
                                                                 }

                                                                 try {
                                                                     M(c);
                                                                 } finally {
                                                                     M(c);
                                                                 }

                                                                 switch (c) {
                                                                     case true:
                                                                         break;
                                                                 }

                                                                 void Local() {
                                                                     M(c);
                                                                     M(!c);
                                                                 }

                                                                 Action a = () => {
                                                                     M(c);
                                                                     M(!c);
                                                                 };
                                                                 Action b = delegate { M(c); };
                                                                 Register(() => {
                                                                         M(c);
                                                                         M(!c);
                                                                     }
                                                                 );
                                                                 var l = new List<int>
                                                                 {
                                                                     1,
                                                                     2,
                                                                     3,
                                                                     4,
                                                                     5
                                                                 };
                                                                 var o = new { X = 1 };
                                                                 var r = 1 switch
                                                                 {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 };
                                                             }

                                                             void Empty() { }

                                                             void Register(Action a) {
                                                                 a();
                                                                 a();
                                                             }
                                                         }

                                                         enum F {
                                                             G
                                                         }
                                                     }
                                                     """;

    const string None = """
                        using System;
                        using System.Collections.Generic;

                        namespace N {
                            class C {
                                int _n;

                                int A {
                                    get {
                                        _n++;
                                        return _n;
                                    }
                                }

                                int B { get; set; }

                                int D {
                                    get { return _n; }
                                }

                                event Action E {
                                    add {
                                        _n++;
                                        _n++;
                                    }
                                    remove { }
                                }

                                void M(bool c) {
                                    if (c) {
                                        M(c);
                                        M(!c);
                                    } else {
                                        M(!c);
                                    }

                                    try {
                                        M(c);
                                    } finally {
                                        M(c);
                                    }

                                    switch (c) {
                                        case true:
                                            break;
                                    }

                                    void Local() {
                                        M(c);
                                        M(!c);
                                    }

                                    Action a = () => {
                                        M(c);
                                        M(!c);
                                    };
                                    Action b = delegate { M(c); };
                                    Register(() => {
                                            M(c);
                                            M(!c);
                                        }
                                    );
                                    var l = new List<int> {
                                        1,
                                        2,
                                        3,
                                        4,
                                        5
                                    };
                                    var o = new { X = 1 };
                                    var r = 1 switch {
                                        1 => 2,
                                        _ => 3
                                    };
                                }

                                void Empty() { }

                                void Register(Action a) {
                                    a();
                                    a();
                                }
                            }

                            enum F {
                                G
                            }
                        }
                        """;

    public static TheoryData<string, string> Values =>
        new() {
            { "all", All },
            { "types", Types },
            { "methods", Methods },
            { "properties", Properties },
            { "accessors", Accessors },
            { "control_blocks", ControlBlocks },
            { "lambdas", Lambdas },
            { "object_collection_array_initializers", ObjectCollectionArrayInitializers },
            { "none", None }
        };

    /// <summary>
    ///     Every value of the key the C# formatter answers to, and <c>none</c> as the control: the
    ///     brace of a body that breaks open goes on a line of its own, and one whose body stays on its
    ///     owner's line — <c>int B { get; set; }</c>, <c>get { return _n; }</c>, <c>delegate { M(c); }</c>,
    ///     <c>new { X = 1 }</c> — stays there.
    /// </summary>
    [Theory]
    [MemberData(nameof(Values))]
    public void AKAndRInput_ComesBackAsTheOracleWritesIt(string value, string expected) {
        var formatted = FormatWith(KAndR, ("csharp_new_line_before_open_brace", value));
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, ("csharp_new_line_before_open_brace", value)));
    }
}
