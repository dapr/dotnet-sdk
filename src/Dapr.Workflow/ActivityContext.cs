using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DurableTask;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Defines properties and methods for task activity context objects.
    /// </summary>
    public class ActivityContext
    {
        readonly TaskActivityContext innerContext;

        internal ActivityContext(TaskActivityContext innerContext)
        {
            this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
        }

        /// <summary>
        /// Gets the name of the activity.
        /// </summary>
        public TaskName Name => this.innerContext.Name;

        /// <summary>
        /// Gets the unique ID of the current workflow instance.
        /// </summary>
        public string InstanceId => this.innerContext.InstanceId;
    }
}