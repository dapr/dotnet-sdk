// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace RoutingSample;

/// <summary>
/// Represents a transaction used by sample code.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Gets or sets account id for the transaction.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets amount for the transaction.
    /// </summary>
    public decimal Amount { get; set; }
}