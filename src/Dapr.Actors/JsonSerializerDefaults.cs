// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Text.Json;

namespace Dapr.Actors
{
    /// <summary>
    /// A helper used to standardize the <see cref="JsonSerializerOptions"/> defaults
    /// </summary>
    internal static class JsonSerializerDefaults
    {
        /// <summary>
        /// <see cref="JsonSerializerOptions"/> defaults with Camel Casing and case insensitive properties
        /// </summary>
        internal static JsonSerializerOptions Web => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}