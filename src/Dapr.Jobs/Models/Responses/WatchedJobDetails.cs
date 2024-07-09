namespace Dapr.Jobs.Models.Responses;

/// <summary>
/// Returns information about a watched job.
/// </summary>
/// <typeparam name="T">The type to deserialize the payload to.</typeparam>
/// <param name="Id">The identifier of the job itself - once the job is processed, this should be returned back to the server so it can be finalized.</param>
/// <param name="Name">The name of the job.</param>
/// <param name="Payload">The payload data included with the job.</param>
public record WatchedJobDetails<T>(ulong Id, string Name, T Payload);
