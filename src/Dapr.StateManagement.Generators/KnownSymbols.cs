// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

namespace Dapr.StateManagement;

/// <summary>
/// Caches the <see cref="Microsoft.CodeAnalysis.INamedTypeSymbol"/> references to the
/// attributes and base types this generator depends on. Looked up once per compilation.
/// </summary>
internal sealed record KnownSymbols(
    Microsoft.CodeAnalysis.INamedTypeSymbol? StateStoreAttribute,
    Microsoft.CodeAnalysis.INamedTypeSymbol? IDaprStateStoreClient);
