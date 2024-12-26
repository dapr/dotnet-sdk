using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;

namespace Dapr.Common.Exceptions
{
    /// <summary>
    /// <see cref="DaprExtendedErrorDetail"/> factory.
    /// </summary>
    internal static class ExtendedErrorDetailFactory
    {
        private const string DaprErrorTypeUrl = DaprExtendedErrorConstants.ErrorDetailTypeUrl;

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
        /// <param name="message">The serialized detail message to create the error detail from.</param>
        /// <returns>A new instance of <see cref="DaprExtendedErrorDetail"/></returns>
        internal static DaprExtendedErrorDetail CreateErrorDetail(Any message)
        {
            if (!extendedErrorTypeMapping.TryGetValue(message.TypeUrl, out var create))
            {
                return new DaprUnknownDetail(message.TypeUrl);
            }

            return create.Invoke(message.Value);
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
                FieldViolations = badRequest.FieldViolations.Select(
                    fieldViolation => new DaprBadRequestDetailFieldViolation(Field: fieldViolation.Field, Description: fieldViolation.Description)).ToArray()
            };
        }

        private static DaprErrorInfoDetail ToDaprErrorInfoDetail(ByteString data)
        {
            var errorInfo = ErrorInfo.Parser.ParseFrom(data);
            return new(Reason: errorInfo.Reason, Domain: errorInfo.Domain, Metadata: errorInfo.Metadata);
        }

        private static DaprHelpDetail ToDaprHelpDetail(ByteString data)
        {
            var helpInfo = Help.Parser.ParseFrom(data);
            return new()
            {
                Links = helpInfo.Links.Select(link => new DaprHelpDetailLink(Url: link.Url, Description: link.Description)).ToArray()
            };
        }
    }
}
