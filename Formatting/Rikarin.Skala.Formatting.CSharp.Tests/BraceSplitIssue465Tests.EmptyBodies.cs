namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>Issue #465's empty bodies: the empty-block key beside the brace key.</summary>
public sealed partial class BraceSplitIssue465Tests {
    const string EmptyKAndR = """
                              using System;
                              using System.Collections.Generic;
                              namespace N { }
                              class E { }
                              interface I { }
                              enum F { }
                              class Q {
                                  int _n;
                                  int P { get { } set { } }
                                  void M() { }
                                  void N(bool c, int o) {
                                      if (c) { }
                                      while (c) { }
                                      switch (o) { }
                                      switch (o) {
                                      }
                                      try { } catch { }
                                      Action a = () => { };
                                      Action b = delegate { };
                                      var l = new List<int> { };
                                      var x = new { };
                                      void L() { }
                                      lock (this) { }
                                  }
                              }
                              """;

    const string EmptyAllman = """
                               using System;
                               using System.Collections.Generic;
                               namespace N
                               {
                               }
                               class E
                               {
                               }
                               class Q
                               {
                                   void M()
                                   {
                                   }
                                   void N(bool c, int o)
                                   {
                                       if (c)
                                       {
                                       }
                                       Action a = () =>
                                       {
                                       };
                                       void L()
                                       {
                                       }
                                       var l = new List<int>
                                       {
                                       };
                                       if (c)
                                       { }
                                   }
                               }
                               """;

    const string EmptyAllTogetherKAndR = """
                                         using System;
                                         using System.Collections.Generic;

                                         namespace N
                                         { }

                                         class E
                                         { }

                                         interface I
                                         { }

                                         enum F
                                         { }

                                         class Q
                                         {
                                             int _n;

                                             int P
                                             {
                                                 get { }
                                                 set { }
                                             }

                                             void M()
                                             { }

                                             void N(bool c, int o)
                                             {
                                                 if (c)
                                                 { }

                                                 while (c)
                                                 { }

                                                 switch (o)
                                                 { }

                                                 switch (o)
                                                 { }

                                                 try
                                                 { } catch
                                                 { }

                                                 Action a = () => { };
                                                 Action b = delegate { };
                                                 var l = new List<int> { };
                                                 var x = new { };

                                                 void L()
                                                 { }

                                                 lock (this)
                                                 { }
                                             }
                                         }
                                         """;

    const string EmptyAllTogetherAllman = """
                                          using System;
                                          using System.Collections.Generic;

                                          namespace N
                                          { }

                                          class E
                                          { }

                                          class Q
                                          {
                                              void M()
                                              { }

                                              void N(bool c, int o)
                                              {
                                                  if (c)
                                                  { }

                                                  Action a = () => { };

                                                  void L()
                                                  { }

                                                  var l = new List<int> { };
                                                  if (c)
                                                  { }
                                              }
                                          }
                                          """;

    const string EmptyAllTogetherSameLineKAndR = """
                                                 using System;
                                                 using System.Collections.Generic;

                                                 namespace N { }

                                                 class E { }

                                                 interface I { }

                                                 enum F { }

                                                 class Q
                                                 {
                                                     int _n;

                                                     int P
                                                     {
                                                         get { }
                                                         set { }
                                                     }

                                                     void M() { }

                                                     void N(bool c, int o)
                                                     {
                                                         if (c) { }

                                                         while (c) { }

                                                         switch (o) { }

                                                         switch (o) { }

                                                         try { } catch { }

                                                         Action a = () => { };
                                                         Action b = delegate { };
                                                         var l = new List<int> { };
                                                         var x = new { };
                                                         void L() { }
                                                         lock (this) { }
                                                     }
                                                 }
                                                 """;

    const string EmptyAllTogetherSameLineAllman = """
                                                  using System;
                                                  using System.Collections.Generic;

                                                  namespace N { }

                                                  class E { }

                                                  class Q
                                                  {
                                                      void M() { }

                                                      void N(bool c, int o)
                                                      {
                                                          if (c) { }

                                                          Action a = () => { };
                                                          void L() { }
                                                          var l = new List<int> { };
                                                          if (c) { }
                                                      }
                                                  }
                                                  """;

    const string EmptyAllMultilineKAndR = """
                                          using System;
                                          using System.Collections.Generic;

                                          namespace N
                                          {
                                          }

                                          class E
                                          {
                                          }

                                          interface I
                                          {
                                          }

                                          enum F
                                          {
                                          }

                                          class Q
                                          {
                                              int _n;

                                              int P
                                              {
                                                  get { }
                                                  set { }
                                              }

                                              void M()
                                              {
                                              }

                                              void N(bool c, int o)
                                              {
                                                  if (c)
                                                  {
                                                  }

                                                  while (c)
                                                  {
                                                  }

                                                  switch (o)
                                                  {
                                                  }

                                                  switch (o)
                                                  {
                                                  }

                                                  try
                                                  {
                                                  } catch
                                                  {
                                                  }

                                                  Action a = () => { };
                                                  Action b = delegate { };
                                                  var l = new List<int> { };
                                                  var x = new { };

                                                  void L()
                                                  {
                                                  }

                                                  lock (this)
                                                  {
                                                  }
                                              }
                                          }
                                          """;

    const string EmptyAllMultilineAllman = """
                                           using System;
                                           using System.Collections.Generic;

                                           namespace N
                                           {
                                           }

                                           class E
                                           {
                                           }

                                           class Q
                                           {
                                               void M()
                                               {
                                               }

                                               void N(bool c, int o)
                                               {
                                                   if (c)
                                                   {
                                                   }

                                                   Action a = () => { };

                                                   void L()
                                                   {
                                                   }

                                                   var l = new List<int> { };
                                                   if (c)
                                                   {
                                                   }
                                               }
                                           }
                                           """;

    const string EmptyNoneTogetherKAndR = """
                                          using System;
                                          using System.Collections.Generic;

                                          namespace N { }

                                          class E { }

                                          interface I { }

                                          enum F { }

                                          class Q {
                                              int _n;

                                              int P {
                                                  get { }
                                                  set { }
                                              }

                                              void M() { }

                                              void N(bool c, int o) {
                                                  if (c) { }

                                                  while (c) { }

                                                  switch (o) { }

                                                  switch (o) { }

                                                  try { } catch { }

                                                  Action a = () => { };
                                                  Action b = delegate { };
                                                  var l = new List<int> { };
                                                  var x = new { };
                                                  void L() { }
                                                  lock (this) { }
                                              }
                                          }
                                          """;

    const string EmptyNoneTogetherAllman = """
                                           using System;
                                           using System.Collections.Generic;

                                           namespace N { }

                                           class E { }

                                           class Q {
                                               void M() { }

                                               void N(bool c, int o) {
                                                   if (c) { }

                                                   Action a = () => { };
                                                   void L() { }
                                                   var l = new List<int> { };
                                                   if (c) { }
                                               }
                                           }
                                           """;

    const string EmptyNoneTogetherSameLineKAndR = """
                                                  using System;
                                                  using System.Collections.Generic;

                                                  namespace N { }

                                                  class E { }

                                                  interface I { }

                                                  enum F { }

                                                  class Q {
                                                      int _n;

                                                      int P {
                                                          get { }
                                                          set { }
                                                      }

                                                      void M() { }

                                                      void N(bool c, int o) {
                                                          if (c) { }

                                                          while (c) { }

                                                          switch (o) { }

                                                          switch (o) { }

                                                          try { } catch { }

                                                          Action a = () => { };
                                                          Action b = delegate { };
                                                          var l = new List<int> { };
                                                          var x = new { };
                                                          void L() { }
                                                          lock (this) { }
                                                      }
                                                  }
                                                  """;

    const string EmptyNoneTogetherSameLineAllman = """
                                                   using System;
                                                   using System.Collections.Generic;

                                                   namespace N { }

                                                   class E { }

                                                   class Q {
                                                       void M() { }

                                                       void N(bool c, int o) {
                                                           if (c) { }

                                                           Action a = () => { };
                                                           void L() { }
                                                           var l = new List<int> { };
                                                           if (c) { }
                                                       }
                                                   }
                                                   """;

    const string EmptyNoneMultilineKAndR = """
                                           using System;
                                           using System.Collections.Generic;

                                           namespace N {
                                           }

                                           class E {
                                           }

                                           interface I {
                                           }

                                           enum F {
                                           }

                                           class Q {
                                               int _n;

                                               int P {
                                                   get { }
                                                   set { }
                                               }

                                               void M() {
                                               }

                                               void N(bool c, int o) {
                                                   if (c) {
                                                   }

                                                   while (c) {
                                                   }

                                                   switch (o) {
                                                   }

                                                   switch (o) {
                                                   }

                                                   try {
                                                   } catch {
                                                   }

                                                   Action a = () => { };
                                                   Action b = delegate { };
                                                   var l = new List<int> { };
                                                   var x = new { };

                                                   void L() {
                                                   }

                                                   lock (this) {
                                                   }
                                               }
                                           }
                                           """;

    const string EmptyNoneMultilineAllman = """
                                            using System;
                                            using System.Collections.Generic;

                                            namespace N {
                                            }

                                            class E {
                                            }

                                            class Q {
                                                void M() {
                                                }

                                                void N(bool c, int o) {
                                                    if (c) {
                                                    }

                                                    Action a = () => { };

                                                    void L() {
                                                    }

                                                    var l = new List<int> { };
                                                    if (c) {
                                                    }
                                                }
                                            }
                                            """;

    public static TheoryData<string, string, string, string> EmptyBodies =>
        new() {
            { "all", "together", EmptyKAndR, EmptyAllTogetherKAndR },
            { "all", "together", EmptyAllman, EmptyAllTogetherAllman },
            { "all", "together_same_line", EmptyKAndR, EmptyAllTogetherSameLineKAndR },
            { "all", "together_same_line", EmptyAllman, EmptyAllTogetherSameLineAllman },
            { "all", "multiline", EmptyKAndR, EmptyAllMultilineKAndR },
            { "all", "multiline", EmptyAllman, EmptyAllMultilineAllman },
            { "none", "together", EmptyKAndR, EmptyNoneTogetherKAndR },
            { "none", "together", EmptyAllman, EmptyNoneTogetherAllman },
            { "none", "together_same_line", EmptyKAndR, EmptyNoneTogetherSameLineKAndR },
            { "none", "together_same_line", EmptyAllman, EmptyNoneTogetherSameLineAllman },
            { "none", "multiline", EmptyKAndR, EmptyNoneMultilineKAndR },
            { "none", "multiline", EmptyAllman, EmptyNoneMultilineAllman }
        };

    /// <summary>
    ///     An empty body under <c>skala_empty_block_style</c>, written both ways, at both ends of the brace
    ///     key.
    /// </summary>
    /// <remarks>
    ///     ⚠ A type's, a namespace's, a method's, a local function's, a control block's and a switch's
    ///     empty body follows the empty-block key — <c>together</c> puts <c>{ }</c> on a line of its own
    ///     under <c>all</c>, <c>together_same_line</c> pulls it back onto the owner's line, and
    ///     <c>multiline</c> splits a <c>{ }</c> the author wrote joined, under <c>none</c> as well. An
    ///     accessor's, a lambda's, an anonymous method's and an initializer's stays <c>{ }</c> on its
    ///     owner's line at every value, and is joined back from Allman.
    /// </remarks>
    [Theory]
    [MemberData(nameof(EmptyBodies))]
    public void AnEmptyBody_ComesBackAsTheOracleWritesIt(string brace, string empty, string source, string expected) {
        var formatted = FormatWith(
            source,
            ("csharp_new_line_before_open_brace", brace),
            ("skala_empty_block_style", empty)
        );

        Assert.Equal(expected + "\n", formatted);
    }
}
