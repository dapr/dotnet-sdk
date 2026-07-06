// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors.Communication;

using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using Dapr.Actors.Resources;

[DataContract(Name = "ActorHeader", Namespace = Constants.Namespace)]
internal class ActorRequestMessageHeader : IActorRequestMessageHeader
{
    internal const string CancellationHeaderName = "CancellationHeader";

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorRequestMessageHeader"/> class.
    /// </summary>
    public ActorRequestMessageHeader()
    {
        this.headers = new Dictionary<string, byte[]>();
        this.InvocationId = null;
    }

    /// <summary>
    /// Gets or sets the methodId of the remote method.
    /// </summary>
    /// <value>Method id.</value>
    [DataMember(Name = "MethodId", IsRequired = true, Order = 0)]
    public int MethodId { get; set; }

    /// <summary>
    /// Gets or sets the interface id of the remote interface.
    /// </summary>
    /// <value>Interface id.</value>
    [DataMember(Name = "InterfaceId", IsRequired = true, Order = 1)]
    public int InterfaceId { get; set; }

    /// <summary>
    /// Gets or sets identifier for the remote method invocation.
    /// </summary>
    [DataMember(Name = "InvocationId", IsRequired = false, Order = 2, EmitDefaultValue = false)]
    public string InvocationId { get; set; }

    [DataMember(IsRequired = false, Order = 3)]
    public ActorId ActorId { get; set; }

    [DataMember(IsRequired = false, Order = 4)]
    public string CallContext { get; set; }

    [DataMember(Name = "Headers", IsRequired = true, Order = 5)]

#pragma warning disable SA1201 // Elements should appear in the correct order. Increases readbility when fields kept in order.
    private readonly Dictionary<string, byte[]> headers;
#pragma warning restore SA1201 // Elements should appear in the correct order

    /// <summary>
    /// Gets or sets the method name of the remote method.
    /// </summary>
    /// <value>Method Name.</value>
    [DataMember(Name = "MethodName", IsRequired = false, Order = 6)]
    public string MethodName { get; set; }

    [DataMember(IsRequired = false, Order = 7)]
    public string ActorType { get; set; }

    public void AddHeader(string headerName, byte[] headerValue)
    {
        if (this.headers.ContainsKey(headerName))
        {
            // TODO throw specific translated exception type
            throw new System.Exception(
                string.Format(
                    CultureInfo.CurrentCulture,
                    SR.ErrorHeaderAlreadyExists,
                    headerName));
        }

        this.headers[headerName] = headerValue;
    }

    public bool TryGetHeaderValue(string headerName, out byte[] headerValue)
    {
        headerValue = null;

        if (this.headers == null)
        {
            return false;
        }

        return this.headers.TryGetValue(headerName, out headerValue);
    }
}