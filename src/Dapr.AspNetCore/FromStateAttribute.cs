// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Mvc
{
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
        /// <param name="stateStoreName">The state store name.</param>
        public FromStateAttribute(string stateStoreName)
        {
            this.StateStoreName = stateStoreName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FromStateAttribute"/> class.
        /// </summary>
        /// <param name="stateStoreName">The state store name.</param>
        /// <param name="key">The state key.</param>
        public FromStateAttribute(string stateStoreName, string key)
        {
            this.StateStoreName = stateStoreName;
            this.Key = key;
        }

        /// <summary>
        /// Gets the state store name.
        /// </summary>
        public string StateStoreName { get; }

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
                return new FromStateBindingSource(this.StateStoreName, this.Key);
            }
        }
    }
}