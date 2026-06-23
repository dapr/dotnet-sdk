; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DAPR1405 | Usage | Error | Actor interface should inherit from IActor.
DAPR1406 | Usage | Warning | Enum members in Actor types should use EnumMember attribute.
DAPR1407 | Usage | Info | Consider using JsonPropertyName for property name consistency.
DAPR1408 | Usage | Warning | Complex types used in Actor methods need serialization attributes.
DAPR1409 | Usage | Warning | Actor method parameter needs proper serialization attributes.
DAPR1410 | Usage | Warning | Actor method return type needs proper serialization attributes.
DAPR1411 | Usage | Warning | Collection types in Actor methods need element type validation.
DAPR1412 | Usage | Warning | Record types should use DataContract and DataMember attributes for Actor serialization.
DAPR1413 | Usage | Error | Actor class implementation should implement an interface that inherits from IActor.
DAPR1414 | Usage | Error | All types must either expose a public parameterless constructor or be decorated with the DataContractAttribute attribute.