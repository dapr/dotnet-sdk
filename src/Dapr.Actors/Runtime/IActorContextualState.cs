// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;

namespace Dapr.Actors.Runtime
{
    /// <summary>
    ///
    /// </summary>
    public interface IActorContextualState
    {
        /// <summary>
        /// </summary>
        Task SetStateContext(string stateContext);
    }
}