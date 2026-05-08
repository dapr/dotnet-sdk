// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.StateManagement;

// Annotate a partial interface with [StateStore("storeName")] to have the Dapr source
// generator automatically create:
//   - A sealed internal implementation that forwards all IDaprStateStoreClient calls to
//     DaprStateManagementClient with the store name pre-filled ("statestore").
//   - A DI extension method: IDaprStateManagementBuilder.AddWidgetStore()

/// <summary>
/// Typed state store client bound to the "statestore" Dapr component.
/// The implementation is generated at compile time by the Dapr.StateManagement.Generators
/// source generator.
/// </summary>
[StateStore("statestore")]
public partial interface IWidgetStore : IDaprStateStoreClient;
