// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Mvc
{
    using System;
    using System.ComponentModel;
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
}