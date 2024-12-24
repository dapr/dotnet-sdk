namespace Dapr.Common.Exceptions
{
    /// <summary>
    /// Dapr implementation of the richer error model.
    /// </summary>
    /// <param name="Code">A status code.</param>
    /// <param name="Message">A message.</param>
    public sealed record DaprExtendedErrorInfo(int Code, string Message)
    {
        /// <summary>
        /// A collection of details that provide more information on the error.
        /// </summary>
        public DaprExtendedErrorDetail[] Details { get; init; } = Array.Empty<DaprExtendedErrorDetail>();
    }
}
