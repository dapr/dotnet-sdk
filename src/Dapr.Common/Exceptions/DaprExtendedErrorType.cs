namespace Dapr.Common.Exceptions
{
    /// <summary>
    /// Extended Error Detail Types.
    /// </summary>
    public enum DaprExtendedErrorType
    {
        /// <summary>
        /// Unrecognized Extended Error Type.
        /// </summary>
        Unrecognized,

        /// <summary>
        /// Retry Info Detail Type.
        /// </summary>
        RetryInfo,

        /// <summary>
        /// Debug Info Detail Type.
        /// </summary>
        DebugInfo,

        /// <summary>
        /// Quote Failure Detail Type.
        /// </summary>
        QuotaFailure,

        /// <summary>
        /// Precondition Failure Detail Type.
        /// </summary>
        PreconditionFailure,

        /// <summary>
        /// Request Info Detail Type.
        /// </summary>
        RequestInfo,

        /// <summary>
        /// Localized Message Detail Type.
        /// </summary>
        LocalizedMessage,

        /// <summary>
        /// Bad Request Detail Type.
        /// </summary>
        BadRequest,

        /// <summary>
        /// Error Info Detail Type.
        /// </summary>
        ErrorInfo,

        /// <summary>
        /// Help Detail Type.
        /// </summary>
        Help,

        /// <summary>
        /// Resource Info Detail Type.
        /// </summary>
        ResourceInfo
    }
}
