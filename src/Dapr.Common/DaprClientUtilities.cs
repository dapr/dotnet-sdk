using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using Grpc.Core;

namespace Dapr.Common;

internal static class DaprClientUtilities
{
    private const byte TraceContextVersion = 0;
    private const byte TraceIdFieldId = 0;
    private const byte SpanIdFieldId = 1;
    private const byte TraceOptionsFieldId = 2;

    private const int TraceIdLength = 16;
    private const int SpanIdLength = 8;
    private const int GrpcTraceBinHeaderLength = 29;
    private const string GrpcTraceBinHeader = "grpc-trace-bin";

    /// <summary>
    /// Provisions the gRPC call options used to provision the various Dapr clients.
    /// </summary>
    /// <param name="daprApiToken">The Dapr API token, if any.</param>
    /// <param name="assembly">The assembly the user agent is built from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The gRPC call options.</returns>
    internal static CallOptions ConfigureGrpcCallOptions(Assembly assembly, string? daprApiToken, CancellationToken cancellationToken = default)
    {
        var callOptions = new CallOptions(headers: [], cancellationToken: cancellationToken);
        
        //Add the user-agent header to the gRPC call options
        var assemblyVersion = assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;
        var userAgent = new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}").ToString();
        callOptions.Headers!.Add("User-Agent", userAgent);

        //Add the API token to the headers as well if it's populated
        if (daprApiToken is not null)
        {
            var apiTokenHeader = GetDaprApiTokenHeader(daprApiToken);
            if (apiTokenHeader is not null)
            {
                callOptions.Headers.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);
            }
        }

        AddCurrentTraceContextHeaders(callOptions.Headers);

        return callOptions;
    }

    /// <summary>
    /// Adds the current W3C trace context to gRPC metadata when an ambient activity exists.
    /// </summary>
    /// <param name="headers">The gRPC metadata headers.</param>
    internal static void AddCurrentTraceContextHeaders(Metadata headers)
    {
        var activity = Activity.Current;
        if (activity?.IdFormat != ActivityIdFormat.W3C || string.IsNullOrEmpty(activity.Id))
        {
            return;
        }

        if (!headers.Any(header => string.Equals(header.Key, GrpcTraceBinHeader, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(GrpcTraceBinHeader, CreateGrpcTraceBinHeader(activity));
        }
    }

    private static byte[] CreateGrpcTraceBinHeader(Activity activity)
    {
        // grpc-trace-bin format:
        // {version}{trace-id-field}{trace-id}{span-id-field}{span-id}{trace-options-field}{trace-options}
        // See:
        // https://github.com/census-instrumentation/opencensus-specs/blob/master/encodings/BinaryEncoding.md
        var header = new byte[GrpcTraceBinHeaderLength];

        var offset = 0;

        header[offset++] = TraceContextVersion;

        header[offset++] = TraceIdFieldId;
        activity.TraceId.CopyTo(header.AsSpan(offset, TraceIdLength));
        offset += TraceIdLength;

        header[offset++] = SpanIdFieldId;
        activity.SpanId.CopyTo(header.AsSpan(offset, SpanIdLength));
        offset += SpanIdLength;

        header[offset++] = TraceOptionsFieldId;
        header[offset] = (byte)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded);

        return header;
    }

    /// <summary>
    /// Used to create the user-agent from the assembly attributes.
    /// </summary>
    /// <param name="assembly">The assembly the client is being built for.</param>
    /// <returns>The header value containing the user agent information.</returns>
    public static ProductInfoHeaderValue GetUserAgent(Assembly assembly)
    {
        var assemblyVersion = assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;
        return new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}");
    }

    /// <summary>
    /// Used to provision the header used for the Dapr API token on the HTTP or gRPC connection.
    /// </summary>
    /// <param name="daprApiToken">The value of the Dapr API token.</param>
    /// <returns>If a Dapr API token exists, the key/value pair to use for the header; otherwise null.</returns>
    public static KeyValuePair<string, string>? GetDaprApiTokenHeader(string? daprApiToken) =>
        string.IsNullOrWhiteSpace(daprApiToken)
            ? null
            : new KeyValuePair<string, string>("dapr-api-token", daprApiToken);
}
