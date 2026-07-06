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
using System.Reflection;
using Dapr.Actors.Description;

internal class MethodBodyTypesBuilder : CodeBuilderModule
{
    public MethodBodyTypesBuilder(ICodeBuilder codeBuilder)
        : base(codeBuilder)
    {
    }

    public MethodBodyTypesBuildResult Build(InterfaceDescription interfaceDescription)
    {
        var context = new CodeBuilderContext(
            assemblyName: this.CodeBuilder.Names.GetMethodBodyTypesAssemblyName(interfaceDescription.InterfaceType),
            assemblyNamespace: this.CodeBuilder.Names.GetMethodBodyTypesAssemblyNamespace(interfaceDescription.InterfaceType),
            enableDebugging: CodeBuilderAttribute.IsDebuggingEnabled(interfaceDescription.InterfaceType));

        var result = new MethodBodyTypesBuildResult(context)
        {
            MethodBodyTypesMap = new Dictionary<string, MethodBodyTypes>(),
        };
        foreach (var method in interfaceDescription.Methods)
        {
            result.MethodBodyTypesMap.Add(
                method.Name,
                Build(this.CodeBuilder.Names, context, method));
        }

        context.Complete();
        return result;
    }

    private static MethodBodyTypes Build(
        ICodeBuilderNames codeBuilderNames,
        CodeBuilderContext context,
        MethodDescription methodDescription)
    {
        var methodDataTypes = new MethodBodyTypes()
        {
            RequestBodyType = null,
            ResponseBodyType = null,
            HasCancellationTokenArgument = methodDescription.HasCancellationToken,
        };

        if ((methodDescription.Arguments != null) && (methodDescription.Arguments.Length != 0))
        {
            methodDataTypes.RequestBodyType = BuildRequestBodyType(codeBuilderNames, context, methodDescription);
        }

        if (TypeUtility.IsTaskType(methodDescription.ReturnType) && methodDescription.ReturnType.GetTypeInfo().IsGenericType)
        {
            methodDataTypes.ResponseBodyType = BuildResponseBodyType(codeBuilderNames, context, methodDescription);
        }

        return methodDataTypes;
    }

    private static Type BuildRequestBodyType(
        ICodeBuilderNames codeBuilderNames,
        CodeBuilderContext context,
        MethodDescription methodDescription)
    {
        var requestBodyTypeBuilder = CodeBuilderUtils.CreateDataContractTypeBuilder(
            moduleBuilder: context.ModuleBuilder,
            ns: context.AssemblyNamespace,
            typeName: codeBuilderNames.GetRequestBodyTypeName(methodDescription.Name),
            dcNamespace: codeBuilderNames.GetDataContractNamespace());

        foreach (var argument in methodDescription.Arguments)
        {
            CodeBuilderUtils.AddDataMemberField(
                dcTypeBuilder: requestBodyTypeBuilder,
                fieldType: argument.ArgumentType,
                fieldName: argument.Name);
        }

        return requestBodyTypeBuilder.CreateTypeInfo().AsType();
    }

    private static Type BuildResponseBodyType(
        ICodeBuilderNames codeBuilderNames,
        CodeBuilderContext context,
        MethodDescription methodDescription)
    {
        var responseBodyTypeBuilder = CodeBuilderUtils.CreateDataContractTypeBuilder(
            moduleBuilder: context.ModuleBuilder,
            ns: context.AssemblyNamespace,
            typeName: codeBuilderNames.GetResponseBodyTypeName(methodDescription.Name),
            dcNamespace: codeBuilderNames.GetDataContractNamespace());

        var returnDataType = methodDescription.ReturnType.GetGenericArguments()[0];
        CodeBuilderUtils.AddDataMemberField(
            dcTypeBuilder: responseBodyTypeBuilder,
            fieldType: returnDataType,
            fieldName: codeBuilderNames.RetVal);

        return responseBodyTypeBuilder.CreateTypeInfo().AsType();
    }
}