// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
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
}
