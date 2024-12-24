using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;

namespace Dapr.Common.Exceptions
{
    /// <summary>
    /// <see cref="DaprExtendedErrorDetail"/> factory class.
    /// </summary>
    internal static class ExtendedErrorDetailFactory
    {
        private const string DaprErrorTypeUrl = "type.googleapis.com/";

        private static Dictionary<string, Func<ByteString, DaprExtendedErrorDetail>> extendedErrorTypeMapping =
            new()
            {
                { $"{DaprErrorTypeUrl}Google.rpc.ErrorInfo", ToDaprErrorInfoDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.RetryInfo", ToDaprRetryInfoDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.DebugInfo", ToDaprDebugInfoDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.QuotaFailure", ToDaprQuotaFailureDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.PreconditionFailure", ToDaprPreconditionFailureDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.BadRequest", ToDaprBadRequestDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.RequestInfo", ToDaprRequestInfoDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.ResourceInfo", ToDaprResourceInfoDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.Help", ToDaprHelpDetail },
                { $"{DaprErrorTypeUrl}Google.rpc.LocalizedMessage", ToDaprLocalizedMessageDetail },
            };

        /// <summary>
        /// Create a new <see cref="DaprExtendedErrorDetail"/> from an instance of <see cref="Any"/>.
        /// </summary>
        /// <param name="metadata">The <see cref="Any"/> to create <see cref="DaprExtendedErrorDetail"/> from.</param>
        /// <returns>A new instance of <see cref="DaprExtendedErrorDetail"/></returns>
        internal static DaprExtendedErrorDetail CreateErrorDetail(Any metadata)
        {
            if (!extendedErrorTypeMapping.TryGetValue(metadata.TypeUrl, out var create))
            {
                return new DaprUnrecognizedDetail(metadata.TypeUrl);
            }

            return create.Invoke(metadata.Value);
        }

        private static DaprRetryInfoDetail ToDaprRetryInfoDetail(ByteString data)
        {
            var retryInfo = RetryInfo.Parser.ParseFrom(data);
            return new() { Delay = new DaprRetryDelay(Seconds: retryInfo.RetryDelay.Seconds, Nanos: retryInfo.RetryDelay.Nanos) } ;
        }

        private static DaprLocalizedMessageDetail ToDaprLocalizedMessageDetail(ByteString data)
        {
            var localizedMessage = LocalizedMessage.Parser.ParseFrom(data);
            return new(Locale: localizedMessage.Locale, Message: localizedMessage.Message);
        }

        private static DaprDebugInfoDetail ToDaprDebugInfoDetail(ByteString data)
        {
            var debugInfo = DebugInfo.Parser.ParseFrom(data);
            return new(StackEntries: debugInfo.StackEntries.ToArray(), Detail: debugInfo.Detail);
        }

        private static DaprQuotaFailureDetail ToDaprQuotaFailureDetail(ByteString data)
        {
            var quotaFailure = QuotaFailure.Parser.ParseFrom(data);
            return new()
            {
                Violations = quotaFailure.Violations.Select(violation => new DaprQuotaFailureViolation(Subject: violation.Subject, Description: violation.Description)).ToArray(),
            };
        }

        private static DaprPreconditionFailureDetail ToDaprPreconditionFailureDetail(ByteString data)
        {
            var preconditionFailure = PreconditionFailure.Parser.ParseFrom(data);
            return new()
            {
                Violations = preconditionFailure.Violations.Select(violation => new DaprPreconditionFailureViolation(Type: violation.Type, Subject: violation.Subject, Description: violation.Description)).ToArray()
            };
        }

        private static DaprRequestInfoDetail ToDaprRequestInfoDetail(ByteString data)
        {
            var requestInfo = RequestInfo.Parser.ParseFrom(data);
            return new(RequestId: requestInfo.RequestId, ServingData: requestInfo.ServingData);
        }

        private static DaprResourceInfoDetail ToDaprResourceInfoDetail(ByteString data)
        {
            var resourceInfo = ResourceInfo.Parser.ParseFrom(data);
            return new(ResourceType: resourceInfo.ResourceType, ResourceName: resourceInfo.ResourceName, Owner: resourceInfo.Owner, Description: resourceInfo.Description);
        }

        private static DaprBadRequestDetail ToDaprBadRequestDetail(ByteString data)
        {
            var badRequest = BadRequest.Parser.ParseFrom(data);
            return new()
            {
                FieldViolations = badRequest.FieldViolations.Select(fieldViolation => new DaprBadRequestDetailFieldViolation(fieldViolation.Field, fieldViolation.Description)).ToArray()
            };
        }

        private static DaprErrorInfoDetail ToDaprErrorInfoDetail(ByteString data)
        {
            var errorInfo = ErrorInfo.Parser.ParseFrom(data);
            return new(Reason: errorInfo.Reason, Domain: errorInfo.Domain);
        }

        private static DaprHelpDetail ToDaprHelpDetail(ByteString data)
        {
            var helpInfo = Help.Parser.ParseFrom(data);
            return new()
            {
                Links = helpInfo.Links.Select(link => new DaprHelpDetailLink(link.Url, link.Description)).ToArray()
            };
        }
    }
}
