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