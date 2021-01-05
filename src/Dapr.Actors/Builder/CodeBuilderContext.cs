// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System.Reflection.Emit;

    internal class CodeBuilderContext
    {
        private readonly bool enableDebugging;

        public CodeBuilderContext(string assemblyName, string assemblyNamespace, bool enableDebugging = false)
        {
            this.AssemblyNamespace = assemblyNamespace;
            this.enableDebugging = enableDebugging;

            this.AssemblyBuilder = CodeBuilderUtils.CreateAssemblyBuilder(assemblyName);
            this.ModuleBuilder = CodeBuilderUtils.CreateModuleBuilder(this.AssemblyBuilder, assemblyName);
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
