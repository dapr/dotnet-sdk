// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace GrpcServiceSample.Models
{
    /// <summary>
    /// BankService GetAccount input model
    /// </summary>
    public class GetAccountInput
    {
        /// <summary>
        /// Id of account
        /// </summary>
        public string Id { get; set; }
    }
}
