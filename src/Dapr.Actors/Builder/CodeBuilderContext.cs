// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Builder;

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