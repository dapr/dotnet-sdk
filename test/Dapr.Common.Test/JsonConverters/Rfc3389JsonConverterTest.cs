// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapr.Common.JsonConverters;
using Xunit;

namespace Dapr.Common.Test.JsonConverters;

public sealed class Rfc3389JsonConverterTest
{
    private readonly Rfc3389JsonConverter _converter = new();

    [Fact]
    public void Read_ShouldReturnNull_WhenStringValueIsNull()
    {
        const string json = "null";
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        reader.Read();

        var result = _converter.Read(ref reader, typeof(DateTimeOffset?), new JsonSerializerOptions());

        Assert.Null(result);
    }
    
    [Fact]
    public void Write_ShouldWriteNullValue_WhenValueIsNull()
    {
        var options = new JsonSerializerOptions();
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        _converter.Write(writer, null, options);
        writer.Flush();
 
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("null", json);
    }
    
    [Fact]
    public void Write_ShouldWriteStringValue_WhenValueIsNotNull()
    {
        var options = new JsonSerializerOptions();
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var dateTimeOffset = DateTimeOffset.ParseExact("2025-04-13T06:35:22.000Z", "yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        _converter.Write(writer, dateTimeOffset, options);
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("\"2025-04-13T06:35:22.000Z\"", json);
    }
}
