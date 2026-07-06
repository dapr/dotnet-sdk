// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace Dapr.E2E.Test;

// Assertion methods for HTTP requests
public static class HttpAssert
{
    public static async Task<(TValue value, string json)> AssertJsonResponseAsync<TValue>(HttpResponseMessage response)
    {
        if (response.Content is null && !response.IsSuccessStatusCode)
        {
            var message = $"The response had status code {response.StatusCode} and no body."; 
            throw new XunitException(message);
        }
        else if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            var message = $"The response had status code {response.StatusCode} and body: " + Environment.NewLine + text; 
            throw new XunitException(message);
        }
        else
        {
            // Use the string to read from JSON so we can include it in the error if it fails.
            var text = await response.Content.ReadAsStringAsync();

            try
            {
                var value = JsonSerializer.Deserialize<TValue>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return (value, text);
            }
            catch (JsonException ex)
            {
                var message = $"The response failed to deserialize to a {typeof(TValue).Name} from JSON with error: '{ex.Message}' and body: " + Environment.NewLine + text; 
                throw new XunitException(message);
            }
        }
    }
}