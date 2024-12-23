using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Dapr.Common.Exceptions
{
    /// <summary>
    /// ExtendedErrorInfoTypes
    /// </summary>
    public enum DaprExtendedErrorType
    {
        /// <summary>
        /// Unrecognized Extended Error Type
        /// </summary>
        Unrecognized,

        /// <summary>
        /// Retry Info
        /// </summary>
        RetryInfo,

        /// <summary>
        /// DebugInfo
        /// </summary>
        DebugInfo,

        /// <summary>
        /// QuotaFailure
        /// </summary>
        QuotaFailure,

        /// <summary>
        /// PreconditionFailure
        /// </summary>
        PreconditionFailure,

        /// <summary>
        /// RequestInfo
        /// </summary>
        RequestInfo,

        /// <summary>
        /// LocalizedMessage
        /// </summary>
        LocalizedMessage,

        /// <summary>
        /// Bad request.
        /// </summary>
        BadRequest,

        /// <summary>
        /// Info relating to the exception.
        /// </summary>
        ErrorInfo,

        /// <summary>
        /// Help URL
        /// </summary>
        Help,

        /// <summary>
        /// ResourceInfo
        /// </summary>
        ResourceInfo

    }

    /// <summary>
    /// Base class of the Dapr extended error detail.
    /// </summary>
    public abstract record DaprExtendedErrorDetail(DaprExtendedErrorType ErrorType);

    /// <summary>
    /// An unrecognized detail.
    /// </summary>
    /// <param name="TypeUrl">Type Url</param>
    public sealed record DaprUnrecognizedDetail(string TypeUrl) : DaprExtendedErrorDetail(DaprExtendedErrorType.Unrecognized);

    /// <summary>
    /// A Debug Info detail.
    /// </summary>
    /// <param name="StackEntries">Stack Entries.</param>
    /// <param name="Detail">Detail</param>
    public sealed record DaprDebugInfoDetail(string[] StackEntries, string Detail) : DaprExtendedErrorDetail(DaprExtendedErrorType.DebugInfo);

    /// <summary>
    /// Precondition Violation
    /// </summary>
    /// <param name="Type">PreconditionType</param>
    /// <param name="Subject"></param>
    /// <param name="Description"></param>
    public sealed record DaprPreconditionFailureViolation(string Type, string Subject, string Description);

    /// <summary>
    /// Precondition Failure
    /// </summary>
    public sealed record DaprPreconditionFailureDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.PreconditionFailure)
    {
        /// <summary>
        /// Collection of violations
        /// </summary>
        public DaprPreconditionFailureViolation[] Violations { get; init; } = Array.Empty<DaprPreconditionFailureViolation>();
    }

    /// <summary>
    /// Retry info
    /// </summary>
    /// <param name="Seconds">seconds</param>
    /// <param name="Nanos">nanos</param>
    public sealed record DaprRetryInfoDetail(long Seconds, int Nanos) : DaprExtendedErrorDetail(DaprExtendedErrorType.RetryInfo);

    /// <summary>
    /// Quota Violation
    /// </summary>
    /// <param name="Subject"></param>
    /// <param name="Description"></param>
    public sealed record DaprQuotaFailureViolation(string Subject, string Description);

    /// <summary>
    /// Quota Failure
    /// </summary>
    public sealed record DaprQuotaFailureDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.QuotaFailure)
    {
        /// <summary>
        /// Collection of violations
        /// </summary>
        public DaprQuotaFailureViolation[] Violations { get; init; } = Array.Empty<DaprQuotaFailureViolation>();
    }

    /// <summary>
    /// Bad Request Field Violation
    /// </summary>
    /// <param name="Field"></param>
    /// <param name="Description"></param>
    public sealed record DaprBadRequestDetailFieldViolation(string Field, string Description);

    /// <summary>
    /// Dapr bad request details
    /// </summary>
    public sealed record DaprBadRequestDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.BadRequest)
    {
        /// <summary>
        /// Field Violations.
        /// </summary>
        public DaprBadRequestDetailFieldViolation[] FieldViolations { get; init; } = Array.Empty<DaprBadRequestDetailFieldViolation>();
    }

    /// <summary>
    /// RequestInfo
    /// </summary>
    /// <param name="RequestId">RequestId.</param>
    /// <param name="ServingData">ServingData.</param>
    public sealed record DaprRequestInfoDetail(string RequestId, string ServingData) : DaprExtendedErrorDetail(DaprExtendedErrorType.RequestInfo);

    /// <summary>
    /// Localized Message.
    /// </summary>
    /// <param name="Locale">Locale.</param>
    /// <param name="Message">Message.</param>
    public sealed record DaprLocalizedMessageDetail(string Locale, string Message) : DaprExtendedErrorDetail(DaprExtendedErrorType.LocalizedMessage);


    /// <summary>
    /// Link
    /// </summary>
    /// <param name="Url"></param>
    /// <param name="Description"></param>
    public sealed record DaprHelpDetailLink(string Url, string Description);

    /// <summary>
    /// Dapr help details
    /// </summary>
    public sealed record DaprHelpDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.Help)
    {
        /// <summary>
        /// Links
        /// </summary>
        public DaprHelpDetailLink[] Links { get; init; } = Array.Empty<DaprHelpDetailLink>();
    }


    /// <summary>
    /// Dapr resource info details
    /// </summary>
    public sealed record DaprResourceInfoDetail(string ResourceType, string ResourceName, string Owner, string Description) : DaprExtendedErrorDetail(DaprExtendedErrorType.ResourceInfo);

    /// <summary>
    /// Dapr resource info details
    /// </summary>
    /// <param name="Reason">Reason</param>
    /// <param name="Domain">Domain</param>
    public sealed record DaprErrorInfoDetail(string Reason, string Domain) : DaprExtendedErrorDetail(DaprExtendedErrorType.ErrorInfo);

    /// <summary>
    /// Extended error detail
    /// </summary>
    /// <param name="Code"></param>
    /// <param name="Message"></param>
    public sealed record DaprExtendedErrorInfo(int Code, string Message)
    {
        /// <summary>
        /// 
        /// </summary>
        public DaprExtendedErrorDetail[] Details { get; init; } = Array.Empty<DaprExtendedErrorDetail>();
    }

    /// <summary>
    /// Provides help extension methods for <see cref="DaprException"/>
    /// </summary>
    public static class DaprExceptionExtensions
    {
        private static string GrpcDetails = "grpc-status-details-bin";

        private const string DaprErrorTypeUrl = "type.googleapis.com/";

        private const string ErrorInfo = $"{DaprErrorTypeUrl}Google.rpc.ErrorInfo";
        private const string RetryInfo = $"{DaprErrorTypeUrl}Google.rpc.RetryInfo";
        private const string DebugInfo = $"{DaprErrorTypeUrl}Google.rpc.DebugInfo";
        private const string QuotaFailure = $"{DaprErrorTypeUrl}Google.rpc.QuotaFailure";
        private const string PreconditionFailure = $"{DaprErrorTypeUrl}Google.rpc.PreconditionFailure";
        private const string BadRequest = $"{DaprErrorTypeUrl}Google.rpc.BadRequest";
        private const string RequestInfo = $"{DaprErrorTypeUrl}Google.rpc.RequestInfo";
        private const string ResourceInfo = $"{DaprErrorTypeUrl}Google.rpc.ResourceInfo";
        private const string Help = $"{DaprErrorTypeUrl}Google.rpc.Help";
        private const string LocalizedMessage = $"{DaprErrorTypeUrl}Google.rpc.LocalizedMessage";

        /// <summary>
        /// Attempt to retrieve <see cref="DaprExtendedErrorInfo"/> from <see cref="DaprException"/>
        /// </summary>
        /// <returns></returns>
        public static bool TryGetExtendedErrorInfo(this DaprException exception, [NotNullWhen(true)] out DaprExtendedErrorInfo? daprExtendedErrorInfo)
        {
            daprExtendedErrorInfo = null;
            if (exception.InnerException is not RpcException rpcException)
            {
                return false;
            }

            var metadata = rpcException.Trailers.Get(GrpcDetails);

            if (metadata is null)
            {
                return false;
            }

            var status = Google.Rpc.Status.Parser.ParseFrom(metadata.ValueBytes);

            daprExtendedErrorInfo = new DaprExtendedErrorInfo(status.Code, status.Message)
            {
                Details = status.Details.Select(detail => GetDaprErrorDetailFromType(detail)).ToArray(),
            };

            return true;
        }

        private static DaprExtendedErrorDetail GetDaprErrorDetailFromType(Any detail)
        {
            return detail.TypeUrl switch
            {
                ErrorInfo => ToDaprErrorInfoDetail(Google.Rpc.ErrorInfo.Parser.ParseFrom(detail.Value)),
                RetryInfo => ToDaprRetryInfoDetail(Google.Rpc.RetryInfo.Parser.ParseFrom(detail.Value)),
                DebugInfo => ToDaprDebugInfoDetail(Google.Rpc.DebugInfo.Parser.ParseFrom(detail.Value)),
                QuotaFailure => ToDaprQuotaFailureDetail(Google.Rpc.QuotaFailure.Parser.ParseFrom(detail.Value)),
                PreconditionFailure => ToDaprPreconditionFailureDetail(Google.Rpc.PreconditionFailure.Parser.ParseFrom(detail.Value)),
                BadRequest => ToDaprBadRequestDetail(Google.Rpc.BadRequest.Parser.ParseFrom(detail.Value)),
                RequestInfo => ToDaprRequestInfoDetail(Google.Rpc.RequestInfo.Parser.ParseFrom(detail.Value)),
                ResourceInfo => ToDaprResourceInfoDetail(Google.Rpc.ResourceInfo.Parser.ParseFrom(detail.Value)),
                Help => ToDaprHelpDetail(Google.Rpc.Help.Parser.ParseFrom(detail.Value)),
                LocalizedMessage => ToDaprLocalizedMessageDetail(Google.Rpc.LocalizedMessage.Parser.ParseFrom(detail.Value)),
                _ => new DaprUnrecognizedDetail(detail.TypeUrl),
            };
        }

        private static DaprRetryInfoDetail ToDaprRetryInfoDetail(Google.Rpc.RetryInfo retryInfo) =>
            new(Seconds: retryInfo.RetryDelay.Seconds, Nanos: retryInfo.RetryDelay.Nanos);

        private static DaprLocalizedMessageDetail ToDaprLocalizedMessageDetail(Google.Rpc.LocalizedMessage localizedMessage) =>
            new(Locale: localizedMessage.Locale, Message: localizedMessage.Message);

        private static DaprDebugInfoDetail ToDaprDebugInfoDetail(Google.Rpc.DebugInfo debugInfo) =>
            new(StackEntries: debugInfo.StackEntries.ToArray(), Detail: debugInfo.Detail);

        private static DaprQuotaFailureDetail ToDaprQuotaFailureDetail(Google.Rpc.QuotaFailure quotaFailure) =>
            new()
            {
                Violations = quotaFailure.Violations.Select(violation =>  new DaprQuotaFailureViolation(Subject: violation.Subject, Description: violation.Description)).ToArray(),
            };

        private static DaprPreconditionFailureDetail ToDaprPreconditionFailureDetail(Google.Rpc.PreconditionFailure preconditionFailure) =>
            new() { Violations = preconditionFailure.Violations.Select(violation => new DaprPreconditionFailureViolation(Type: violation.Type, Subject: violation.Subject, Description: violation.Description)).ToArray() };

        private static DaprRequestInfoDetail ToDaprRequestInfoDetail(Google.Rpc.RequestInfo requestInfo) =>
            new(RequestId: requestInfo.RequestId, ServingData: requestInfo.ServingData);

        private static DaprResourceInfoDetail ToDaprResourceInfoDetail(Google.Rpc.ResourceInfo resourceInfo) =>
            new(ResourceType: resourceInfo.ResourceType, ResourceName: resourceInfo.ResourceName, Owner: resourceInfo.Owner, Description: resourceInfo.Description);

        private static DaprBadRequestDetail ToDaprBadRequestDetail(Google.Rpc.BadRequest badRequest) =>
            new()
            {
                FieldViolations = badRequest.FieldViolations.Select(fieldViolation =>
                new DaprBadRequestDetailFieldViolation(fieldViolation.Field, fieldViolation.Description)).ToArray()
            };

        private static DaprErrorInfoDetail ToDaprErrorInfoDetail(Google.Rpc.ErrorInfo errorInfo) =>
            new(Reason: errorInfo.Reason, Domain: errorInfo.Domain);

        private static DaprHelpDetail ToDaprHelpDetail(Google.Rpc.Help help) =>
            new()
            {
                Links = help.Links.Select(link => new DaprHelpDetailLink(link.Url, link.Description)).ToArray()
            };
    }
}
