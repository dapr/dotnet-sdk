// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    internal class FromStateBindingSource : BindingSource
    {
        public FromStateBindingSource(string stateStoreName, string key)
            : base("state", "Dapr state store", isGreedy: true, isFromRequest: false)
        {
            this.StateStoreName = stateStoreName;
            this.Key = key;
        }

        public string StateStoreName { get; }

        public string Key { get; }
    }
}