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

using Dapr.Search;

namespace SearchExample;

// These names are fixed at compile time; generated methods do not need them as arguments.
[SearchIndex("searchstore", "generated-search-example-products")]
public partial interface IProductSearch : IDaprSearchIndex<Product>;

public sealed record Product(string Title, decimal Price);
