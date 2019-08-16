// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Builder
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Actions.Actors.Description;

    internal abstract class CodeBuilderModule
    {
        protected CodeBuilderModule(ICodeBuilder codeBuilder)
        {
            this.CodeBuilder = codeBuilder;
        }

        protected ICodeBuilder CodeBuilder { get; }

        protected static IReadOnlyDictionary<int, string> GetMethodNameMap(InterfaceDescription interfaceDescription)
        {
            var methodNameMap = interfaceDescription.Methods.ToDictionary(
                methodDescription => methodDescription.Id,
                methodDescription => methodDescription.Name);

            return new ReadOnlyDictionary<int, string>(methodNameMap);
        }
    }
}
