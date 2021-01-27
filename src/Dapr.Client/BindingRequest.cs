// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Represents the request used to invoke a binding.
    /// </summary>
    public sealed class BindingRequest
    {
        /// <summary>
        /// Initializes a new <see cref="BindingRequest" /> for the provided <paramref name="bindingName" /> and 
        /// <paramref name="operation" />.
        /// </summary>
        /// <param name="bindingName">The name of the binding.</param>
        /// <param name="operation">The type of operation to perform on the binding.</param>
        public BindingRequest(string bindingName, string operation)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(bindingName, nameof(bindingName));
            ArgumentVerifier.ThrowIfNullOrEmpty(operation, nameof(operation));

            this.BindingName = bindingName;
            this.Operation = operation;

            this.Metadata = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the name of the binding.
        /// </summary>
        /// <value></value>
        public string BindingName { get; }

        /// <summary>
        /// Gets the type of operation to perform on the binding.
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Gets or sets the binding request payload.
        /// </summary>
        public ReadOnlyMemory<byte> Data { get; set; }

        /// <summary>
        /// Gets the metadata; a collection of metadata key-value pairs that will be provided to the binding. 
        /// The valid metadata keys and values are determined by the type of binding used.
        /// </summary>
        public Dictionary<string, string> Metadata { get; }
    }
}
