namespace Dapr.Common.Generators.Models;

/// <summary>
/// How the generator handles fallback for a method group.
/// </summary>
internal enum MethodClassification
{
    /// <summary>
    /// Only one variant exists; the generated wrapper simply forwards the call.
    /// </summary>
    PassThrough,

    /// <summary>
    /// Multiple variants exist and request/response types are field-compatible, so the
    /// generator can auto-produce the fallback chain with field mapping.
    /// </summary>
    AutoCompatible,

    /// <summary>
    /// Multiple variants exist but the schemas differ significantly enough that automatic
    /// field mapping is not safe. The wrapper checks capability and throws
    /// <see cref="System.NotSupportedException"/> on the older path unless a partial-class
    /// override is provided.
    /// </summary>
    SchemaDivergent
}
