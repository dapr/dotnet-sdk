// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.Serialization;

    [DataContract(Name = "ServiceResponseMessageHeaders", Namespace = Constants.ServiceCommunicationNamespace)]

    internal class ResponseMessageHeader : IResponseMessageHeader
    {
        [DataMember(Name = "Headers", IsRequired = true, Order = 2)]
        private Dictionary<string, byte[]> headers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseMessageHeader"/> class.
        /// </summary>
        public ResponseMessageHeader()
        {
            this.headers = new Dictionary<string, byte[]>();
        }

        public void AddHeader(string headerName, byte[] headerValue)
        {
            if (this.headers.ContainsKey(headerName))
            {
                // TODO throw actions specific translated exception type
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
