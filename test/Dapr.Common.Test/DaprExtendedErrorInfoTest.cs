using System.Collections.Generic;
using Dapr.Common.Exceptions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Xunit;

namespace Dapr.Common.Test;

public class DaprExtendedErrorInfoTest
{
    private static int statusCode = 1;
    private static string statusMessage = "Status Message";

    [Fact]
    public void DaprExendedErrorInfo_ThrowsRpcDaprException_ExtendedErrorInfoReturnsTrueAndNotNull()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        BadRequest badRequest = new();

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.BadRequest", Value = badRequest.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "BadRequest"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);
    }

    [Fact]
    public void DaprExendedErrorInfo_ThrowsNonRpcDaprException_ExtendedErrorInfoReturnsFalseAndIsNull()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;

        // Act, Assert
        try
        {
            throw new DaprException("Non-Rpc based Dapr exception");
        }

        catch (DaprException daprEx)
        {
            Assert.False(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.Null(result);
    }

    [Fact]
    public void DaprExendedErrorInfo_ThrowsRpcDaprException_NoTrailers_ExtendedErrorInfoIsNull()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;

        var rpcEx = new RpcException(status: new Grpc.Core.Status());

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.False(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.Null(result);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsBadRequestRpcException_ShouldGetSingleDaprBadRequestDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        BadRequest badRequest = new();

        var violationDescription = "Violation Description";
        var violationField = "Violation Field";

        badRequest.FieldViolations.Add(new BadRequest.Types.FieldViolation()
        {
            Description = violationDescription,
            Field = violationField,
        });

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.BadRequest", Value = badRequest.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "BadRequest"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);
        var detail = Assert.Single(result.Details);

        Assert.Equal(DaprExtendedErrorType.BadRequest, detail.ErrorType);
        var badRequestDetail = Assert.IsType<DaprBadRequestDetail>(detail);

        var violation = Assert.Single(badRequestDetail.FieldViolations);

        Assert.Equal(violation.Description, violationDescription);
        Assert.Equal(violation.Field, violationField);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsLocalizedMessageRpcException_ShouldGetSingleDaprLocalizedMessageDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        var localizedMessage = "Localized Message";
        var locale = "locale";

        LocalizedMessage badRequest = new()
        {
            Message = localizedMessage,
            Locale = locale,
        };

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.LocalizedMessage", Value = badRequest.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "BadRequest"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);
        var detail = Assert.Single(result.Details);
        Assert.Equal(DaprExtendedErrorType.LocalizedMessage, detail.ErrorType);

        var localizedMessageDetail = Assert.IsType<DaprLocalizedMessageDetail>(detail);

        Assert.Equal(localizedMessage, localizedMessageDetail.Message);
        Assert.Equal(locale, localizedMessageDetail.Locale);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowRetryInfoRpcException_ShouldGetSingleDaprRetryInfoDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        RetryInfo retryInfo = new();

        retryInfo.RetryDelay = new Duration()
        {
            Seconds = 1,
            Nanos = 0,
        };

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.RetryInfo", Value = retryInfo.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.FailedPrecondition, "RetryInfo"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);
        var detail = Assert.Single(result.Details);
        Assert.Equal(DaprExtendedErrorType.RetryInfo, detail.ErrorType);

        var retryInfoDetail = Assert.IsType<DaprRetryInfoDetail>(detail);

        Assert.NotNull(retryInfoDetail);
        Assert.Equal(1, retryInfoDetail.Delay.Seconds);
        Assert.Equal(0, retryInfoDetail.Delay.Nanos);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsDebugInfoRpcException_ShouldGetSingleDaprDebugInfoDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        Google.Rpc.DebugInfo debugInfo = new();

        debugInfo.Detail = "Debug Detail";
        debugInfo.StackEntries.Add("Stack Entry");

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.DebugInfo", Value = debugInfo.ToByteString() });

        Grpc.Core.Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(Grpc.Core.StatusCode.FailedPrecondition, "DebugInfo"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);
        var detail = Assert.Single(result.Details);

        Assert.Equal(DaprExtendedErrorType.DebugInfo, detail.ErrorType);

        var daprDebugInfoDetail = Assert.IsType<DaprDebugInfoDetail>(detail);

        Assert.Equal("Debug Detail", daprDebugInfoDetail.Detail);
        var entry = Assert.Single(daprDebugInfoDetail.StackEntries);
        Assert.Equal("Stack Entry", entry);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsPreconditionFailureRpcException_ShouldGetSingleDaprPreconditionFailureDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        Google.Rpc.PreconditionFailure failure = new();

        var violationDesc = "Violation Description";
        var violationSubject = "Violation Subject";
        var violationType = "Violation Type";

        failure.Violations.Add(new Google.Rpc.PreconditionFailure.Types.Violation()
        {
            Description = violationDesc,
            Subject = violationSubject,
            Type = violationType
        });

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.PreconditionFailure", Value = failure.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(Grpc.Core.StatusCode.FailedPrecondition, "PrecondtionFailure"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);

        var detail = Assert.Single(result.Details);
        Assert.Equal(DaprExtendedErrorType.PreconditionFailure, detail.ErrorType);
        var preconditionFailureDetail = Assert.IsType<DaprPreconditionFailureDetail>(detail);

        var violation = Assert.Single(preconditionFailureDetail.Violations);

        Assert.Equal(violation.Description, violationDesc);
        Assert.Equal(violation.Subject, violationSubject);
        Assert.Equal(violation.Type, violationType);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsHelpRpcException_ShouldGetSingleDaprHelpDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        Help help = new();

        var helpDesc = "Help Description";

        var helpUrl = "help-link.com";

        help.Links.Add(new Help.Types.Link()
        {
            Description = helpDesc,
            Url = helpUrl,
        });

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.Help", Value = help.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "Help"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);
        var detail = Assert.Single(result.Details);

        Assert.IsAssignableFrom<DaprExtendedErrorDetail>(detail);
        Assert.Equal(DaprExtendedErrorType.Help, detail.ErrorType);

        var helpDetail = Assert.IsType<DaprHelpDetail>(detail);

        var link = Assert.Single(helpDetail.Links);

        Assert.Equal(helpDesc, link.Description);
        Assert.Equal(helpUrl, link.Url);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsResourceInfoRpcException_ShouldGetSingleDaprResourceInfoDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        var resourceInfoDesc = "Description";
        var resourceInfoOwner = "Owner";
        var resourceInfoName = "Name";
        var resourceInfoType = "Type";

        ResourceInfo resourceInfo = new()
        {
            Description = resourceInfoDesc,
            Owner = resourceInfoOwner,
            ResourceName = resourceInfoName,
            ResourceType = resourceInfoType,
        };


        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.ResourceInfo", Value = resourceInfo.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "ResourceInfo"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);

        var detail = Assert.Single(result.Details);
        Assert.IsAssignableFrom<DaprExtendedErrorDetail>(detail);
        Assert.Equal(DaprExtendedErrorType.ResourceInfo, detail.ErrorType);

        var daprResourceInfo = Assert.IsType<DaprResourceInfoDetail>(detail);

        Assert.Equal(resourceInfoDesc, daprResourceInfo.Description);
        Assert.Equal(resourceInfoName, daprResourceInfo.ResourceName);
        Assert.Equal(resourceInfoType, daprResourceInfo.ResourceType);
        Assert.Equal(resourceInfoOwner, daprResourceInfo.Owner);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsQuotaFailureRpcException_ShouldGetSingleDaprQuotaFailureDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        var quotaFailureDesc = "Description";
        var quotaFailureSubject = "Subject";

        QuotaFailure quotaFailure = new();

        quotaFailure.Violations.Add(new QuotaFailure.Types.Violation()
        {
            Description = quotaFailureDesc,
            Subject = quotaFailureSubject,
        });

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.QuotaFailure", Value = quotaFailure.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "QuotaFailure"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);

        var detail = Assert.Single(result.Details);

        Assert.IsAssignableFrom<DaprExtendedErrorDetail>(detail);
        Assert.Equal(DaprExtendedErrorType.QuotaFailure, detail.ErrorType);

        var quotaFailureDetail = Assert.IsType<DaprQuotaFailureDetail>(detail);

        var violation = Assert.Single(quotaFailureDetail.Violations);
        Assert.Equal(quotaFailureDesc, violation.Description);
        Assert.Equal(quotaFailureSubject, violation.Subject);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsErrorInfoRpcException_ShouldGetSingleDaprErrorInfoDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        var errorInfoDomain = "Domain";
        var errorInfoReason = "Reason";
        var errorInfoMetadataKey = "Key";
        var errorInfoMetadataValue = "Value";
        var errorInfoMetadata = new Dictionary<string, string>()
        {
            { errorInfoMetadataKey, errorInfoMetadataValue }
        };

        Google.Rpc.ErrorInfo errorInfo = new()
        {
            Domain = errorInfoDomain,
            Reason = errorInfoReason,
        };

        errorInfo.Metadata.Add(errorInfoMetadata);

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.ErrorInfo", Value = errorInfo.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "ErrorInfo"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);

        var detail = Assert.Single(result.Details);

        Assert.Equal(DaprExtendedErrorType.ErrorInfo, detail.ErrorType);

        var errorInfoDetail = Assert.IsType<DaprErrorInfoDetail>(detail);

        Assert.Equal(errorInfoDomain, errorInfoDetail.Domain);
        Assert.Equal(errorInfoReason, errorInfoDetail.Reason);

        var metadata = Assert.Single(errorInfoDetail.Metadata);
        Assert.Equal(errorInfoMetadataKey, metadata.Key);
        Assert.Equal(errorInfoMetadataValue, metadata.Value);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsRequestInfoRpcException_ShouldGetSingleDaprRequestInfoDetail()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        var requestInfoId = "RequestId";
        var requestInfoServingData = "Serving Data";

        RequestInfo requestInfo = new()
        {
            RequestId = requestInfoId,
            ServingData = requestInfoServingData,
        };

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.RequestInfo", Value = requestInfo.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "RequestInfo"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.NotNull(result);
        var detail = Assert.Single(result.Details);

        Assert.Equal(DaprExtendedErrorType.RequestInfo, detail.ErrorType);
        var requestInfoDetail = Assert.IsType<DaprRequestInfoDetail>(detail);

        Assert.Equal(requestInfoId, requestInfoDetail.RequestId);
        Assert.Equal(requestInfoServingData, requestInfoDetail.ServingData);
    }

    [Fact]
    public void DaprExtendedErrorInfo_ThrowsRequestInfoRpcException_ShouldGetMultipleDetails()
    {
        // Arrange
        DaprExtendedErrorInfo result = null;
        var metadataEntry = new Google.Rpc.Status()
        {
            Code = statusCode,
            Message = statusMessage,
        };

        List<System.Type> expectedDetailTypes = new()
        {
            typeof(DaprResourceInfoDetail),
            typeof(DaprRequestInfoDetail),
        };

        RequestInfo requestInfo = new();

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.RequestInfo", Value = requestInfo.ToByteString() });

        ResourceInfo resourceInfo = new();

        metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.ResourceInfo", Value = resourceInfo.ToByteString() });

        Metadata trailers = new()
        {
            { DaprExtendedErrorConstants.GrpcDetails, metadataEntry.ToByteArray() }
        };

        var rpcEx = new RpcException(status: new Grpc.Core.Status(StatusCode.Aborted, "RequestInfo"), trailers: trailers);

        // Act, Assert
        try
        {
            ThrowsRpcBasedDaprException(rpcEx);
        }

        catch (DaprException daprEx)
        {
            Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
        }

        Assert.Collection(result.Details,
            detail => Assert.Contains(detail.GetType(), expectedDetailTypes),
            detail => Assert.Contains(detail.GetType(), expectedDetailTypes)
        );
    }

    private static void ThrowsRpcBasedDaprException(RpcException ex) => throw new DaprException("A Dapr exception", ex);
}