// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Builder
{
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
}
