using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Client
{
    /// <summary>
    /// Abstraction around a configuration source.
    /// </summary>
    public abstract class ConfigurationSource : IAsyncEnumerable<IEnumerable<ConfigurationItem>>
    {
        /// <summary>
        /// The Id associated with this configuration source.
        /// </summary>
        public virtual string Id => string.Empty;

        /// <inheritdoc/>
        public abstract IAsyncEnumerator<IEnumerable<ConfigurationItem>> GetAsyncEnumerator(CancellationToken cancellationToken = default);
    }
}
