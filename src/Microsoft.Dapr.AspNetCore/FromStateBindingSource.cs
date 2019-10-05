// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
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