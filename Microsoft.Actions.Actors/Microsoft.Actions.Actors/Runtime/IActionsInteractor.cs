// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for interacting with Actions runtime.
    /// </summary>
    internal interface IActionsInteractor
    {
        /// <summary>
        /// Saves a state to Actions.
        /// </summary>
        /// <param name="key">Name of the state to save.</param>
        /// <param name="value">Value of the state to save.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveStateAsync(string key, object value, CancellationToken cancellationToken = default(CancellationToken));
    }
}
