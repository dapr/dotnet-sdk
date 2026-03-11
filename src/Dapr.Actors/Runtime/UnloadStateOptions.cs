// Options for UnloadStateAsync operation
namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// Options for the UnloadStateAsync operation on ActorStateManager.
    /// </summary>
    public class UnloadStateOptions
    {
        /// <summary>
        /// If true, allows unloading state even if it is modified and not yet persisted.
        /// </summary>
        public bool AllowUnloadingWhenStateModified { get; set; } = false;
    }
}
