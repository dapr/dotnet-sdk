---
type: docs
title: "Richer Error Model in the Dapr .NET SDK"
linkTitle: "Richer error model"
weight: 59000
description: Learn how to use the richer error model in the .NET SDK.
---

The Dapr .NET SDK supports the richer error model, implemented by the Dapr runtime. This model provides a way for applications to enrich their errors with added context,
allowing consumers of the application to better understand the issue and resolve faster. You can read more about the richer error model [here](https://google.aip.dev/193), and you
can find the Dapr proto file implementing these errors [here](https://github.com/googleapis/googleapis/blob/master/google/rpc/error_details.proto").

The Dapr .NET SDK implements all details supported by the Dapr runtime, implemented in the `Dapr.Common.Exceptions` namespace, and is accessible through
the `DaprException` extension method `TryGetExtendedErrorInfo`. Currently this detail extraction is only supported for 
`RpcException`'s where the details are present.

```csharp
// Example usage of ExtendedErrorInfo

try
{
    // Perform some action with the Dapr client that throws a DaprException.
}
catch (DaprException daprEx)
{
    if (daprEx.TryGetExtendedErrorInfo(out DaprExtendedErrorInfo errorInfo)
    {
        Console.WriteLine(errorInfo.Code);
        Console.WriteLine(errorInfo.Message);

        foreach (DaprExtendedErrorDetail detail in errorInfo.Details)
        {
            Console.WriteLine(detail.ErrorType);
            switch (detail.ErrorType)
                case ExtendedErrorType.ErrorInfo:
                    Console.WriteLine(detail.Reason);
                    Console.WriteLine(detail.Domain);
                default:
                    Console.WriteLine(detail.TypeUrl);
        }
    }
}
```

## DaprExtendedErrorInfo

Contains `Code` (the status code) and `Message` (the error message) associated with the error, parsed from an inner `RpcException`.
Also contains a collection of `DaprExtendedErrorDetails` parsed from the details in the exception.

## DaprExtendedErrorDetail

All details implement the abstract `DaprExtendedErrorDetail` and have an associated `DaprExtendedErrorType`.

1. [RetryInfo](#retryinfo)

2. [DebugInfo](#debuginfo)

3. [QuotaFailure](#quotafailure)

4. [PreconditionFailure](#preconditionfailure)

5. [RequestInfo](#requestinfo)

6. [LocalizedMessage](#localizedmessage)

7. [BadRequest](#badrequest)

8. [ErrorInfo](#errorinfo)

9. [Help](#help)

10. [ResourceInfo](#resourceinfo)

11. [Unknown](#unknown)

## RetryInfo

Information telling the client how long to wait before they should retry. Provides a `DaprRetryDelay` with the properties 
`Second` (offset in seconds) and `Nano` (offset in nanoseconds).

## DebugInfo

Debugging information offered by the server. Contains `StackEntries` (a collection of strings containing the stack trace), and 
`Detail` (further debugging information).

## QuotaFailure 

Information relating to some quota that may have been reached, such as a daily usage limit on an API. It has one property `Violations`, 
a collection of `DaprQuotaFailureViolation`, which each contain a `Subject` (the subject of the request) and `Description` (further information regarding the failure).

## PreconditionFailure

Information informing the client that some required precondition was not met. Has one property `Violations`, a collection of 
`DaprPreconditionFailureViolation`, which each has `Subject` (subject where the precondition failure occured e.g. "Azure"), `Type` (representation of the precondition type e.g. "TermsOfService"), and `Description` (further description e.g. "ToS must be accepted.").

## RequestInfo

Information returned by the server that can be used by the server to identify the clients request. Contains
`RequestId` and `ServingData` properties, `RequestId` being some string (such as a UID) the server can interpret,
and `ServingData` being some arbitrary data that made up part of the request.

## LocalizedMessage

Contains a localized message, along with the locale of the message. Contains `Locale` (the locale e.g. "en-US") and `Message` (the localized message).

## BadRequest

Describes a bad request field. Contains collection of `DaprBadRequestDetailFieldViolation`, which each has `Field` (the offending field in request e.g. 'first_name') and
`Description` (further information detailing the reason e.g. "first_name cannot contain special characters").

## ErrorInfo

Details the cause of an error. Contains three properties, `Reason` (the reason for the error, which should take the form of UPPER_SNAKE_CASE e.g. DAPR_INVALID_KEY),
`Domain` (domain the error belongs to e.g. 'dapr.io'), and `Metadata`, a key value based collection of futher information.

## Help

Provides resources for the client to perform further research into the issue. Contains a collection of `DaprHelpDetailLink`,
which provides `Url` (a url to help or documentation), and `Description` (a description of what the link provides).

## ResourceInfo

Provides information relating to an accessed resource. Provides three properties `ResourceType` (type of the resource being access e.g. "Azure service bus"), 
`ResourceName` (The name of the resource e.g. "my-configured-service-bus"), `Owner` (the owner of the resource e.g. "subscriptionowner@dapr.io"),
and `Description` (further information on the resource relating to the error e.g. "missing permissions to use this resource").

## Unknown

Returned when the detail type url cannot be mapped to the correct `DaprExtendedErrorDetail` implementation.
Provides one property `TypeUrl` (the type url that could not be parsed e.g. "type.googleapis.com/Google.rpc.UnrecognizedType").










