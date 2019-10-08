// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    internal class FromStateBindingSource : BindingSource
    {
        public FromStateBindingSource(string key)
            : base("state", "Dapr state store", isGreedy: true, isFromRequest: false)
        {
            this.Key = key;
        }

        public string Key { get; }
    }
}