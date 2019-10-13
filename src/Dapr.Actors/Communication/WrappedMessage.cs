// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Runtime.Serialization;
using Dapr.Actors;

/// <summary>
/// This is a marker class indicating the remoting request / response is wrapped or not.
/// </summary>
[DataContract(Name = "msgBodywrapped", Namespace = Constants.Namespace)]
public abstract class WrappedMessage
{
    /// <summary>
    /// Gets or sets  the wrapped object.
    /// </summary>
    [DataMember(Name = "value", IsRequired = true, Order = 1)]
    public object Value
    {
        get;
        set;
    }
}
