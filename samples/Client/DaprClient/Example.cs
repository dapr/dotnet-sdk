// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Samples.Client
{
    public abstract class Example 
    {
        public abstract string DisplayName { get; }

        public abstract Task RunAsync(CancellationToken cancellationToken);
    }
}
