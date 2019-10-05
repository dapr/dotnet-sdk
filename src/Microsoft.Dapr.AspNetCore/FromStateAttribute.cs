// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Mvc
{
    using System;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.Dapr;

    /// <summary>
    /// Attributes a parameter or property as retrieved from the Dapr state store.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class FromStateAttribute : Attribute, IBindingSourceMetadata
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FromStateAttribute" />.
        /// </summary>
        public FromStateAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FromStateAttribute" />.
        /// </summary>
        /// <param name="key">The state key.</param>
        public FromStateAttribute(string key)
        {
            this.Key = key;
        }

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
                return new FromStateBindingSource(this.Key);
            }
        }
    }
}