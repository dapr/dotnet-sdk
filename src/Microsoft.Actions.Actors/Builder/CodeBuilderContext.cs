// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Builder
{
    using System.Reflection.Emit;

    internal class CodeBuilderContext
    {
        private readonly bool enableDebugging;

        public CodeBuilderContext(string assemblyName, string assemblyNamespace, bool enableDebugging = false)
        {
            this.AssemblyNamespace = assemblyNamespace;
            this.enableDebugging = enableDebugging;

            this.AssemblyBuilder = CodeBuilderUtils.CreateAssemblyBuilder(assemblyName, this.enableDebugging);
            this.ModuleBuilder = CodeBuilderUtils.CreateModuleBuilder(this.AssemblyBuilder, assemblyName, this.enableDebugging);
        }

        public AssemblyBuilder AssemblyBuilder { get; }

        public ModuleBuilder ModuleBuilder { get; }

        public string AssemblyNamespace { get; }

        public void Complete()
        {
            if (this.enableDebugging)
            {
#if !DotNetCoreClr
                this.assemblyBuilder.Save(this.assemblyBuilder.GetName().Name + ".dll");
#endif
            }
        }
    }
}
