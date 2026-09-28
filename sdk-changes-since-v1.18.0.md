# SDK changes since `v1.18.0`

This document summarizes the effective SDK changes that landed after `v1.18.0`, grouped by building block.

Notes:
- Changes are grouped by **building block**.
- Within each section, **fixes** are listed before **new features**.
- Each item includes the **release**, **PR author / PR ID**, **associated issue author / issue ID** when applicable, and any additional **release-note context** that added useful detail beyond the raw PR title.
- CI-only or release-process-only changes are omitted.

## Actors

### Fixes

- **Actors.Next packaging corrected so only the intended top-level package ships/installs, plus more specific exception types**
  - **Release:** effectively part of the Actors.Next stabilization around `v1.18.5` / `v1.19.0-preview.2`
  - **PRs:** [#1868](https://github.com/dapr/dotnet-sdk/pull/1868), [#1869](https://github.com/dapr/dotnet-sdk/pull/1869) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** no extra detail beyond inclusion in the broader Actors.Next promotion.

- **Actors.Next no longer forces SDK defaults when options are unset; daprd defaults can now apply**
  - **Release:** `v1.18.6`
  - **PR:** [#1903](https://github.com/dapr/dotnet-sdk/pull/1903) by @JoshVanL
  - **Issue:** none linked
  - **Release-note context:** no extra prose; release notes categorized it as a feature, but functionally it is a behavior fix.

- **Reentrancy refresh fix for actor state after reentrant saves**
  - **Release:** `v1.18.9`
  - **PR:** [#1912](https://github.com/dapr/dotnet-sdk/pull/1912) by @olitomlinson
  - **Issue:** [dapr/dapr#10532](https://github.com/dapr/dapr/issues/10532) by @olitomlinson
  - **Release-note context:** `v1.18.9` frames this as part of a set of correctness/performance improvements found through external benchmarking.

- **Missing actor-state keys now cache as “not found” instead of round-tripping repeatedly**
  - **Release:** `v1.18.9`
  - **PR:** [#1913](https://github.com/dapr/dotnet-sdk/pull/1913) by @WhitWaldo
  - **Issue:** [#1909](https://github.com/dapr/dotnet-sdk/issues/1909) by @olitomlinson
  - **Release-note context:** `v1.18.9` explicitly notes that this reduced a “not found” case from **44 runtime reads to 1** in the cited benchmark.

- **Actor writes stop doing unnecessary runtime existence checks for upsert-like state writes**
  - **Release:** `v1.18.9`
  - **PR:** [#1914](https://github.com/dapr/dotnet-sdk/pull/1914) by @WhitWaldo
  - **Issue:** [#1910](https://github.com/dapr/dotnet-sdk/issues/1910) by @olitomlinson
  - **Release-note context:** `v1.18.9` says this removed runtime reads during publishing in the benchmarked scenario where there had previously been 21.

- **Regression fix: stale cached `NotFound` entries after a reentrant call creates a key**
  - **Release:** `v1.18.10`
  - **PR:** [#1916](https://github.com/dapr/dotnet-sdk/pull/1916) by @olitomlinson
  - **Issue:** [#1915](https://github.com/dapr/dotnet-sdk/issues/1915) by @olitomlinson
  - **Release-note context:** `v1.18.10` explicitly says this is a **regression introduced in `v1.18.9`**.

### New features

- **`Dapr.Actors.Next` introduced as a new actor SDK**
  - **Release:** `v1.18.5` (also previewed in `v1.19.0-preview.2`)
  - **PR:** [#1865](https://github.com/dapr/dotnet-sdk/pull/1865) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** the preview notes add the most detail here: streaming bidirectional gRPC callbacks instead of per-call inbound handlers, source-generated proxy/dispatch/registration, trim/AOT focus, analyzers/code fixes, deterministic in-memory test runtime, pub/sub-driven actors, dynamic invocation, state-machine support, and published benchmark gains.

- **Actor reminder failure-policy support**
  - **Release:** `v1.18.5`
  - **PR:** [#1872](https://github.com/dapr/dotnet-sdk/pull/1872) by @WhitWaldo
  - **Issue:** [#1679](https://github.com/dapr/dotnet-sdk/issues/1679) by @JoshVanL
  - **Release-note context:** none beyond the item itself.

- **Per-actor-type runtime configuration overrides in Actors.Next**
  - **Release:** `v1.18.6`
  - **PR:** [#1879](https://github.com/dapr/dotnet-sdk/pull/1879) by @m3nax
  - **Issue:** none linked
  - **Release-note context:** none beyond the item itself.

## Workflow

### Fixes

- **`GetWorkflowStateAsync` now always returns `WorkflowState`**
  - **Release:** `v1.18.2`
  - **PR:** [#1847](https://github.com/dapr/dotnet-sdk/pull/1847) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** none beyond the item itself.

- **Fixed workflow versioning / registration ordering**
  - **Release:** `v1.18.2`
  - **PR:** [#1850](https://github.com/dapr/dotnet-sdk/pull/1850) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** none beyond the item itself.

- **Restored workflow analyzer / source-generator compatibility on affected .NET 10 / Roslyn combinations**
  - **Release:** `v1.18.4`
  - **PRs:** [#1851](https://github.com/dapr/dotnet-sdk/pull/1851), [#1854](https://github.com/dapr/dotnet-sdk/pull/1854) by @WhitWaldo
  - **Issue:** [#1853](https://github.com/dapr/dotnet-sdk/issues/1853) by @marcduiker
  - **Release-note context:** none beyond the item list.

- **Workflow tracing now works out of the box, with stronger observability coverage**
  - **Release:** `v1.18.5`
  - **PR:** [#1870](https://github.com/dapr/dotnet-sdk/pull/1870) by @WhitWaldo
  - **Issue:** [#1867](https://github.com/dapr/dotnet-sdk/issues/1867) by @mtaghavi2005
  - **Release-note context:** no extra prose in `v1.18.5`, but the PR and issue make clear the goal was automatic, coherent workflow activity traces without manual source registration.

- **Workflow state/status calls stop swallowing unexpected runtime errors**
  - **Release:** `v1.18.5`
  - **PR:** [#1877](https://github.com/dapr/dotnet-sdk/pull/1877) by @WhitWaldo
  - **Issue:** [#1876](https://github.com/dapr/dotnet-sdk/issues/1876) by @barakbbn
  - **Release-note context:** none beyond the item itself.

- **Fixed workflow generator handling for generic base classes**
  - **Release:** `v1.18.6`
  - **PR:** [#1885](https://github.com/dapr/dotnet-sdk/pull/1885) by @WhitWaldo
  - **Issue:** [#1859](https://github.com/dapr/dotnet-sdk/issues/1859) by @mtaghavi2005
  - **Release-note context:** release notes list this as an issue-specific resolution.

- **Fixed deterministic execution-ID seeding so recreated workflows don’t deadlock on reused IDs**
  - **Release:** `v1.18.6`
  - **PR:** [#1880](https://github.com/dapr/dotnet-sdk/pull/1880) by @JoshVanL
  - **Issue:** none linked
  - **Release-note context:** release notes include it, but under “New Features”; functionally it is a correctness fix.

- **Fixed workflow registration/source generation for abstract workflow classes**
  - **Release:** `v1.18.6`
  - **PR:** [#1902](https://github.com/dapr/dotnet-sdk/pull/1902) by @WhitWaldo
  - **Issue:** [#1898](https://github.com/dapr/dotnet-sdk/issues/1898) by @Kakaosnerk
  - **Release-note context:** release notes list this as an issue-specific resolution.

- **Follow-up hardening of stateful-history delta implementation**
  - **Release:** not yet represented in the `1.18.x` release notes I inspected; merged after that patch line’s published notes
  - **PR:** [#1917](https://github.com/dapr/dotnet-sdk/pull/1917) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** none available from the published release notes used here.

### New features

- **Stateful-history delta support in the workflow worker**
  - **Release:** not called out in GitHub release notes until later follow-up work; landed before `v1.18.10`
  - **PR:** [#1862](https://github.com/dapr/dotnet-sdk/pull/1862) by @JoshVanL
  - **Issue:** none linked
  - **Release-note context:** no release-note summary found for the original landing; the PR itself is the clearest source.

- **Added a testable abstraction/interface for `WorkflowState`**
  - **Release:** `v1.18.6`
  - **PR:** [#1887](https://github.com/dapr/dotnet-sdk/pull/1887) by @WhitWaldo
  - **Issue:** [#1756](https://github.com/dapr/dotnet-sdk/issues/1756) by @SrChronus
  - **Release-note context:** release notes list this as an issue-specific resolution.

## State management

### Fixes

- None in this curated list.

### New features

- **Added outbox-oriented transaction composition helpers and metadata constants**
  - **Release:** `v1.18.6`
  - **PR:** [#1863](https://github.com/dapr/dotnet-sdk/pull/1863) by @Psingle20
  - **Issue:** related issue [#1494](https://github.com/dapr/dotnet-sdk/issues/1494) by @christophdebaene
  - **Release-note context:** release notes label this as a new feature; the PR clarifies it is specifically aimed at composing native transactional outbox state transactions more safely and ergonomically.

## Pub/Sub / Messaging

### Fixes

- **Improved error handling and automatic reconnection for streaming pub/sub subscriptions**
  - **Release:** `v1.18.6`
  - **PR:** [#1888](https://github.com/dapr/dotnet-sdk/pull/1888) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** release notes place it under “New Features,” but the actual behavior change is a resiliency fix.

- **Actors.Next switched to the shared `Dapr.Messaging` pub/sub streaming implementation**
  - **Release:** `v1.18.8`
  - **PR:** [#1907](https://github.com/dapr/dotnet-sdk/pull/1907) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** `v1.18.8` positions this as part of the `Dapr.Messaging` overhaul.

### New features

- **Expanded CloudEvent support with updated fields/constants used by Dapr**
  - **Release:** `v1.18.6`
  - **PR:** [#1889](https://github.com/dapr/dotnet-sdk/pull/1889) by @WhitWaldo
  - **Issue:** [#1161](https://github.com/dapr/dotnet-sdk/issues/1161) by @olitomlinson
  - **Release-note context:** release notes list it as an issue-specific resolution.

- **Completed `Dapr.Messaging` as a full pub/sub-focused package**
  - **Release:** `v1.18.8`
  - **PR:** [#1906](https://github.com/dapr/dotnet-sdk/pull/1906) by @WhitWaldo
  - **Issue:** [#1509](https://github.com/dapr/dotnet-sdk/issues/1509) by @WhitWaldo
  - **Release-note context:** `v1.18.8` adds substantial detail: unified publishing/subscribing, three delivery modes (`Streaming`, `Programmatic`, `Http`), compile-time handler generation, analyzers `DAPR1610`–`DAPR1617`, and a single `AddDaprMessaging()` registration story.

## Jobs

### Fixes

- None in this curated list.

### New features

- **Jobs can now accept timezone-aware cron expressions**
  - **Release:** `v1.18.6`
  - **PR:** [#1884](https://github.com/dapr/dotnet-sdk/pull/1884) by @WhitWaldo
  - **Issue:** [#1883](https://github.com/dapr/dotnet-sdk/issues/1883) by @fcodognotto
  - **Release-note context:** listed as an issue-specific resolution.

- **Added gRPC transport support for Jobs alongside HTTP**
  - **Release:** `v1.18.6`
  - **PR:** [#1886](https://github.com/dapr/dotnet-sdk/pull/1886) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** listed under new features.

- **Added job listing and purge APIs**
  - **Release:** `v1.18.6`
  - **PR:** [#1890](https://github.com/dapr/dotnet-sdk/pull/1890) by @WhitWaldo
  - **Issue:** [#1840](https://github.com/dapr/dotnet-sdk/issues/1840) by @WhitWaldo
  - **Release-note context:** listed as an issue-specific resolution.

## Bindings

### Fixes

- **Stopped sending binary gRPC metadata on `InvokeBinding` calls**
  - **Release:** `v1.18.6`
  - **PR:** [#1901](https://github.com/dapr/dotnet-sdk/pull/1901) by @JoshVanL
  - **Issue:** [#1897](https://github.com/dapr/dotnet-sdk/issues/1897) by @fabistb
  - **Release-note context:** release notes list it as an issue-specific resolution.

### New features

- None in this curated list.

## Secrets / package consumption

### Fixes

- **`Dapr.SecretsManagement` correctly pulls in `Dapr.Common` transitively**
  - **Release:** `v1.18.4`
  - **PR:** [#1852](https://github.com/dapr/dotnet-sdk/pull/1852) by @WhitWaldo
  - **Issue:** none linked
  - **Release-note context:** none beyond the item title.

### New features

- None in this curated list.

## Cross-cutting client / observability

### Fixes

- **Propagated `grpc-trace-bin` so Dapr sidecar gRPC spans parent correctly under the calling application span**
  - **Release:** `v1.18.5`
  - **PR:** [#1858](https://github.com/dapr/dotnet-sdk/pull/1858) by @mtaghavi2005
  - **Issue:** [#1857](https://github.com/dapr/dotnet-sdk/issues/1857) by @mtaghavi2005
  - **Release-note context:** no extra prose in the release note, but the linked issue is explicit that this was a parent/child trace-correlation fix across SDK gRPC calls.

### New features

- None in this curated list.

## Testing / `Dapr.Testcontainers`

### Fixes

- **Raised transitive SSH.NET above the vulnerable range**
  - **Release:** `v1.18.6`
  - **PR:** [#1881](https://github.com/dapr/dotnet-sdk/pull/1881) by @JoshVanL
  - **Issue:** none linked
  - **Release-note context:** `v1.18.6` explicitly calls this out under **Vulnerabilities Fixed**.

- **Fixed stale gRPC/HTTP endpoint mappings after app-first startup retries**
  - **Release:** `v1.18.6`
  - **PR:** [#1896](https://github.com/dapr/dotnet-sdk/pull/1896) by @Copilot
  - **Issue:** none linked
  - **Release-note context:** release notes include this item, though under “New Features”; functionally it is a fix.

### New features

- **Can pass arbitrary environment variables into the `daprd` test container**
  - **Release:** `v1.18.5`
  - **PR:** [#1871](https://github.com/dapr/dotnet-sdk/pull/1871) by @WhitWaldo
  - **Issue:** [#1860](https://github.com/dapr/dotnet-sdk/issues/1860) by @mtaghavi2005
  - **Release-note context:** none beyond the item itself.
