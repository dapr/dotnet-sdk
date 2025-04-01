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

using System;
using System.Collections.Generic;

internal class MethodBodyTypesBuildResult : BuildResult
{
    public MethodBodyTypesBuildResult(CodeBuilderContext buildContext)
        : base(buildContext)
    {
    }

    // methodName, methodBodyTypes (RequestType, ResponseType) map
    public IDictionary<string, MethodBodyTypes> MethodBodyTypesMap { get; set; }

    public IEnumerable<Type> GetRequestBodyTypes()
    {
        var result = new List<Type>();
        if (this.MethodBodyTypesMap != null)
        {
            foreach (var item in this.MethodBodyTypesMap)
            {
                if (item.Value.RequestBodyType != null)
                {
                    result.Add(item.Value.RequestBodyType);
                }
            }
        }

        return result;
    }

    public IEnumerable<Type> GetResponseBodyTypes()
    {
        var result = new List<Type>();
        if (this.MethodBodyTypesMap != null)
        {
            foreach (var item in this.MethodBodyTypesMap)
            {
                if (item.Value.ResponseBodyType != null)
                {
                    result.Add(item.Value.ResponseBodyType);
                }
            }
        }

        return result;
    }
}