#nullable enable
using System.Text.Json;
using Dapr;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// LOL
    /// </summary>
    public static class CloudEventsHttpContextExtensions
    {
        /// <summary>
        /// LOL
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static JsonElement? GetCloudEvent(this HttpContext httpContext)
        {
            return httpContext.Features.Get<ICloudEventFeature>()?.Envelope;
        }
    } 
}
