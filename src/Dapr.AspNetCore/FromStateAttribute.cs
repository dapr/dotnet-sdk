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

namespace Microsoft.AspNetCore.Mvc;

using System;
using Dapr;
using Microsoft.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Attributes a parameter or property as retrieved from the Dapr state store.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class FromStateAttribute : Attribute, IBindingSourceMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FromStateAttribute"/> class.
    /// </summary>
    /// <param name="storeName">The state store name.</param>
    public FromStateAttribute(string storeName)
    {
        if (string.IsNullOrEmpty(storeName))
        {
            throw new ArgumentException("The value cannot be null or empty.", nameof(storeName));
        }

        this.StoreName = storeName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FromStateAttribute"/> class.
    /// </summary>
    /// <param name="storeName">The state store name.</param>
    /// <param name="key">The state key.</param>
    public FromStateAttribute(string storeName, string key)
    {
        this.StoreName = storeName;
        this.Key = key;
    }

    /// <summary>
    /// Gets the state store name.
    /// </summary>
    public string StoreName { get; }

    /// <summary>
    /// Gets the state store key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the <see cref="BindingSource" />.
    /// </summary>
    public BindingSource BindingSource
    {
        get
        {
            return new FromStateBindingSource(this.StoreName, this.Key);
        }
    }
}