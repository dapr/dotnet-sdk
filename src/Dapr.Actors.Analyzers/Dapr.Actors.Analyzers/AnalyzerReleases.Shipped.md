

## Release 1.16.0

### New Rules

Rule ID | Category | Severity | Notes 
--------|----------|----------|------------------------------------------------------------------------------------
DAPR4001 | Usage | Warning | Actor timer method invocations require the named callback method to exist on type.
DAPR4002 | Usage | Warning | The actor type is not registered with dependency injection
DAPR4003 | Usage | Info | Set options.UseJsonSerialization to true to support interoperability with non-.NET actors
DAPR4004 | Usage | Warning | Call app.MapActorsHandlers to map endpoints for Dapr actors.
