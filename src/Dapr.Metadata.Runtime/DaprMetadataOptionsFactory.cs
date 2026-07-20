using Dapr.Metadata.Abstractions;
using Microsoft.Extensions.Options;

namespace Dapr.Metadata.Runtime;

internal sealed class DaprMetadataOptionsFactory(IDaprMetadataProvider provider, IEnumerable<IConfigureOptions<DaprMetadata>> setups, IEnumerable<IPostConfigureOptions<DaprMetadata>> postConfigures, IEnumerable<IValidateOptions<DaprMetadata>> validations)  : OptionsFactory<DaprMetadata>(setups, postConfigures, validations)
{
    /// <inheritdoc />
    protected override DaprMetadata CreateInstance(string name) =>
        provider.GetAsync().AsTask().GetAwaiter().GetResult();
}
