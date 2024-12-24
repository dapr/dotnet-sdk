namespace Dapr.Common.Exceptions
{
    /// <summary>
    /// Base class of the Dapr extended error detail.
    /// </summary>
    public abstract record DaprExtendedErrorDetail(DaprExtendedErrorType ErrorType);

    /// <summary>
    /// An unrecognized detail.
    /// </summary>
    /// <param name="TypeUrl">Type Url.</param>
    public sealed record DaprUnrecognizedDetail(string TypeUrl) : DaprExtendedErrorDetail(DaprExtendedErrorType.Unrecognized);

    /// <summary>
    /// A Debug Info detail.
    /// </summary>
    /// <param name="StackEntries">Stack Entries.</param>
    /// <param name="Detail">Detail.</param>
    public sealed record DaprDebugInfoDetail(string[] StackEntries, string Detail) : DaprExtendedErrorDetail(DaprExtendedErrorType.DebugInfo);

    /// <summary>
    /// A Precondition Violation.
    /// </summary>
    /// <param name="Type">PreconditionType.</param>
    /// <param name="Subject">Subject.</param>
    /// <param name="Description">A Description.</param>
    public sealed record DaprPreconditionFailureViolation(string Type, string Subject, string Description);

    /// <summary>
    /// A Precondition Failure detail.
    /// </summary>
    public sealed record DaprPreconditionFailureDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.PreconditionFailure)
    {
        /// <summary>
        /// Collection of <see cref="DaprBadRequestDetailFieldViolation"/>.
        /// </summary>
        public DaprPreconditionFailureViolation[] Violations { get; init; } = Array.Empty<DaprPreconditionFailureViolation>();
    }

    /// <summary>
    /// Retry information.
    /// </summary>
    /// <param name="Seconds">Second offset.</param>
    /// <param name="Nanos">Nano offset.</param>
    public sealed record DaprRetryDelay(long Seconds, int Nanos);

    /// <summary>
    /// A Retry Info detail.
    /// </summary>
    public sealed record DaprRetryInfoDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.RetryInfo)
    {
        /// <summary>
        /// Provides information of amount of time until retry should be attempted.
        /// </summary>
        public DaprRetryDelay Delay = new(Seconds: 1, Nanos: default);
    }

    /// <summary>
    /// A Quota Violation.
    /// </summary>
    /// <param name="Subject">The Subject.</param>
    /// <param name="Description">A Description.</param>
    public sealed record DaprQuotaFailureViolation(string Subject, string Description);

    /// <summary>
    /// A Quota Failure detail.
    /// </summary>
    public sealed record DaprQuotaFailureDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.QuotaFailure)
    {
        /// <summary>
        /// Collection of <see cref="DaprQuotaFailureViolation"/>.
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
        /// Collection of <see cref="DaprBadRequestDetailFieldViolation"/>.
        /// </summary>
        public DaprBadRequestDetailFieldViolation[] FieldViolations { get; init; } = Array.Empty<DaprBadRequestDetailFieldViolation>();
    }

    /// <summary>
    /// Request Info.
    /// </summary>
    /// <param name="RequestId">A RequestId.</param>
    /// <param name="ServingData">ServingData.</param>
    public sealed record DaprRequestInfoDetail(string RequestId, string ServingData) : DaprExtendedErrorDetail(DaprExtendedErrorType.RequestInfo);

    /// <summary>
    /// Localized Message.
    /// </summary>
    /// <param name="Locale">Locale.</param>
    /// <param name="Message">Message.</param>
    public sealed record DaprLocalizedMessageDetail(string Locale, string Message) : DaprExtendedErrorDetail(DaprExtendedErrorType.LocalizedMessage);


    /// <summary>
    /// A link to help resources.
    /// </summary>
    /// <param name="Url">Url to help details.</param>
    /// <param name="Description">A description.</param>
    public sealed record DaprHelpDetailLink(string Url, string Description);

    /// <summary>
    /// Dapr help details
    /// </summary>
    public sealed record DaprHelpDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.Help)
    {
        /// <summary>
        /// Collection of <see cref="DaprHelpDetailLink"/>.
        /// </summary>
        public DaprHelpDetailLink[] Links { get; init; } = Array.Empty<DaprHelpDetailLink>();
    }


    /// <summary>
    /// Dapr resource info details.
    /// </summary>
    public sealed record DaprResourceInfoDetail(string ResourceType, string ResourceName, string Owner, string Description) : DaprExtendedErrorDetail(DaprExtendedErrorType.ResourceInfo);

    /// <summary>
    /// Dapr error info details.
    /// </summary>
    /// <param name="Reason">Reason</param>
    /// <param name="Domain">Domain</param>
    public sealed record DaprErrorInfoDetail(string Reason, string Domain) : DaprExtendedErrorDetail(DaprExtendedErrorType.ErrorInfo);
}
