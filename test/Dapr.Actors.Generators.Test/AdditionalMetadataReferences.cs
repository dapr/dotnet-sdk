using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Generators;

internal static class AdditionalMetadataReferences
{
    public static readonly MetadataReference Actors = MetadataReference.CreateFromFile(typeof(Dapr.Actors.Client.ActorProxy).Assembly.Location);
}