namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Optional Coyote integration seam. The default package has no Coyote dependency.
/// </summary>
public static class CoyoteBridge
{
    /// <summary>
    /// Gets a value indicating whether this assembly was compiled with the Coyote bridge enabled.
    /// </summary>
#if DAPR_ACTORS_NEXT_COYOTE
    public static bool IsEnabled => true;
#else
    public static bool IsEnabled => false;
#endif
}
