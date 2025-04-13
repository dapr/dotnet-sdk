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

[DataContract(Name = "ActorResponseMessageHeaders", Namespace = Constants.Namespace)]

internal class ActorResponseMessageHeader : IActorResponseMessageHeader
{
    [DataMember(Name = "Headers", IsRequired = true, Order = 2)]
    private readonly Dictionary<string, byte[]> headers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorResponseMessageHeader"/> class.
    /// </summary>
    public ActorResponseMessageHeader()
    {
        this.headers = new Dictionary<string, byte[]>();
    }

    public void AddHeader(string headerName, byte[] headerValue)
    {
        if (this.headers.ContainsKey(headerName))
        {
            // TODO throw Dapr specific translated exception type
            throw new System.Exception(
                string.Format(
                    CultureInfo.CurrentCulture,
                    SR.ErrorHeaderAlreadyExists,
                    headerName));
        }

        this.headers[headerName] = headerValue;
    }

    public bool CheckIfItsEmpty()
    {
        if (this.headers == null || this.headers.Count == 0)
        {
            return true;
        }

        return false;
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