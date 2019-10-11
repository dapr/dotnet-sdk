// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Dapr.Actors.Description;

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
