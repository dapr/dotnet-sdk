// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    internal class FromStateBindingSource : BindingSource
    {
        public FromStateBindingSource(string storeName, string key)
            : base("state", "Dapr state store", isGreedy: true, isFromRequest: false)
        {
            this.StoreName = storeName;
            this.Key = key;
        }

        public string StoreName { get; }

        public string Key { get; }
    }
}