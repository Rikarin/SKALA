// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
#region License

// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

#endregion

#if DNXCORE50
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Newtonsoft.Json.Tests.XUnitAssert;
#else
#endif

namespace Newtonsoft.Json.Tests.Issues;

[TestFixture]
public class Issue1619 : TestFixtureBase {
    [Test]
    public void Test() {
        var value = new Foo { Bar = new(@"c:\temp") };

        string json = JsonConvert.SerializeObject(value, new DirectoryInfoJsonConverter());
        Assert.AreEqual(@"{""Bar"":""c:\\temp""}", json);
    }

    public class Foo {
        public DirectoryInfo Bar { get; set; }
    }

    public class DirectoryInfoJsonConverter : JsonConverter {
        public override bool CanConvert(Type objectType) => objectType == typeof(DirectoryInfo);

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer
        ) {
            if (reader.Value is string s) {
                return new DirectoryInfo(s);
            }

            throw new ArgumentOutOfRangeException(nameof(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (!(value is DirectoryInfo directoryInfo)) {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            writer.WriteValue(directoryInfo.FullName);
        }
    }
}
