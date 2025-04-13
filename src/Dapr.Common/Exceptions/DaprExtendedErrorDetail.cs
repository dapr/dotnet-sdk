// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Common.Exceptions;

/// <summary>
/// Abstract base class of the Dapr extended error detail.
/// </summary>
public abstract record DaprExtendedErrorDetail(DaprExtendedErrorType ErrorType);

/// <summary>
/// Detail when the type url is unrecognized.
/// </summary>
/// <param name="TypeUrl">The unrecognized type url.</param>
public sealed record DaprUnknownDetail(string TypeUrl) : DaprExtendedErrorDetail(DaprExtendedErrorType.Unknown);

/// <summary>
/// Detail proving debugging information.
/// </summary>
/// <param name="StackEntries">Stack trace entries relating to error.</param>
/// <param name="Detail">Further related debugging information.</param>
public sealed record DaprDebugInfoDetail(IReadOnlyCollection<string> StackEntries, string Detail) : DaprExtendedErrorDetail(DaprExtendedErrorType.DebugInfo);

/// <summary>
/// A precondtion violation.
/// </summary>
/// <param name="Type">The type of the violation.</param>
/// <param name="Subject">The subject that the violation relates to.</param>
/// <param name="Description">A description of how the precondition may have failed.</param>
public sealed record DaprPreconditionFailureViolation(string Type, string Subject, string Description);

/// <summary>
/// Detail relating to a failed precondition e.g user has not completed some required check.
/// </summary>
public sealed record DaprPreconditionFailureDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.PreconditionFailure)
{
    /// <summary>
    /// Collection of <see cref="DaprBadRequestDetailFieldViolation"/>.
    /// </summary>
    public IReadOnlyCollection<DaprPreconditionFailureViolation> Violations { get; init; } = Array.Empty<DaprPreconditionFailureViolation>();
}

/// <summary>
/// Provides the time offset the client should use before retrying.
/// </summary>
/// <param name="Seconds">Second offset.</param>
/// <param name="Nanos">Nano offset.</param>
public sealed record DaprRetryDelay(long Seconds, int Nanos);

/// <summary>
/// Detail containing retry information. Provides the minimum amount of the time the client should wait before retrying a request.
/// </summary>
public sealed record DaprRetryInfoDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.RetryInfo)
{
    /// <summary>
    /// A <see cref="DaprRetryDelay"/>.
    /// </summary>
    public DaprRetryDelay Delay = new(Seconds: 1, Nanos: default);
}

/// <summary>
/// Further details relating to a quota violation.
/// </summary>
/// <param name="Subject">The subject where the quota violation occured e.g and ip address or remote resource.</param>
/// <param name="Description">Further information relating to the quota violation.</param>
public sealed record DaprQuotaFailureViolation(string Subject, string Description);

/// <summary>
/// Detail relating to a quota failure e.g reaching API limit.
/// </summary>
public sealed record DaprQuotaFailureDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.QuotaFailure)
{
    /// <summary>
    /// Collection of <see cref="DaprQuotaFailureViolation"/>.
    /// </summary>
    public IReadOnlyCollection<DaprQuotaFailureViolation> Violations { get; init; } = Array.Empty<DaprQuotaFailureViolation>();
}

/// <summary>
/// Further infomation related to a bad request.
/// </summary>
/// <param name="Field">The field that generated the bad request e.g 'NewAccountName||'.</param>
/// <param name="Description">Further description of the field error e.g 'Account name cannot contain '||'' </param>
public sealed record DaprBadRequestDetailFieldViolation(string Field, string Description);

/// <summary>
/// Detail containing information related to a bad request from the client.
/// </summary>
public sealed record DaprBadRequestDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.BadRequest)
{
    /// <summary>
    /// Collection of <see cref="DaprBadRequestDetailFieldViolation"/>.
    /// </summary>
    public IReadOnlyCollection<DaprBadRequestDetailFieldViolation> FieldViolations { get; init; } = Array.Empty<DaprBadRequestDetailFieldViolation>();
}

/// <summary>
/// Detail containing request info used by the client to provide back to the server in relation to filing bugs, providing feedback, or general debugging by the server.
/// </summary>
/// <param name="RequestId">A string understandable by the server e.g an internal UID related a trace.</param>
/// <param name="ServingData">Any data that furthers server debugging.</param>
public sealed record DaprRequestInfoDetail(string RequestId, string ServingData) : DaprExtendedErrorDetail(DaprExtendedErrorType.RequestInfo);

/// <summary>
/// Detail containing a message that can be localized.
/// </summary>
/// <param name="Locale">The locale e.g 'en-US'.</param>
/// <param name="Message">A message to be localized.</param>
public sealed record DaprLocalizedMessageDetail(string Locale, string Message) : DaprExtendedErrorDetail(DaprExtendedErrorType.LocalizedMessage);


/// <summary>
/// Contains a link to a help resource.
/// </summary>
/// <param name="Url">Url to help resources or documentation e.g 'https://v1-15.docs.dapr.io/developing-applications/error-codes/error-codes-reference/'.</param>
/// <param name="Description">A description of the link.</param>
public sealed record DaprHelpDetailLink(string Url, string Description);

/// <summary>
/// Detail containing links to further help resources.
/// </summary>
public sealed record DaprHelpDetail() : DaprExtendedErrorDetail(DaprExtendedErrorType.Help)
{
    /// <summary>
    /// Collection of <see cref="DaprHelpDetailLink"/>.
    /// </summary>
    public IReadOnlyCollection<DaprHelpDetailLink> Links { get; init; } = Array.Empty<DaprHelpDetailLink>();
}

/// <summary>
/// Detail containg resource information.
/// </summary>
/// <param name="ResourceType">The type of the resource e.g 'state'.</param>
/// <param name="ResourceName">The name of the resource e.g 'statestore'.</param>
/// <param name="Owner">The owner of the resource.</param>
/// <param name="Description">Further description of the resource.</param>
public sealed record DaprResourceInfoDetail(string ResourceType, string ResourceName, string Owner, string Description) : DaprExtendedErrorDetail(DaprExtendedErrorType.ResourceInfo);

/// <summary>
/// Detail containing information related to a server error.
/// </summary>
/// <param name="Reason">The error reason e.g 'DAPR_STATE_ILLEGAL_KEY'.</param>
/// <param name="Domain">The error domain e.g 'dapr.io'.</param>
/// <param name="Metadata">Further key / value based metadata.</param>
public sealed record DaprErrorInfoDetail(string Reason, string Domain, IDictionary<string, string>? Metadata) : DaprExtendedErrorDetail(DaprExtendedErrorType.ErrorInfo);