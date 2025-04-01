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

namespace Dapr.Actors.Description;

using System;
using System.Globalization;
using System.Reflection;
using Dapr.Actors.Resources;

internal sealed class MethodArgumentDescription
{
    private readonly ParameterInfo parameterInfo;

    private MethodArgumentDescription(ParameterInfo parameterInfo)
    {
        this.parameterInfo = parameterInfo;
    }

    public string Name
    {
        get { return this.parameterInfo.Name; }
    }

    public Type ArgumentType
    {
        get { return this.parameterInfo.ParameterType; }
    }

    internal static MethodArgumentDescription Create(string remotedInterfaceKindName, MethodInfo methodInfo, ParameterInfo parameter)
    {
        var remotedInterfaceType = methodInfo.DeclaringType;
        EnsureNotOutRefOptional(remotedInterfaceKindName, remotedInterfaceType, methodInfo, parameter);
        EnsureNotVariableLength(remotedInterfaceKindName, remotedInterfaceType, methodInfo, parameter);

        return new MethodArgumentDescription(parameter);
    }

    private static void EnsureNotVariableLength(
        string remotedInterfaceKindName,
        Type remotedInterfaceType,
        MethodInfo methodInfo,
        ParameterInfo param)
    {
        if (param.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
        {
            ThrowArgumentExceptionForParamChecks(
                remotedInterfaceKindName,
                remotedInterfaceType,
                methodInfo,
                param,
                SR.ErrorRemotedMethodHasVarArgParameter);
        }
    }

    private static void EnsureNotOutRefOptional(
        string remotedInterfaceKindName,
        Type remotedInterfaceType,
        MethodInfo methodInfo,
        ParameterInfo param)
    {
        if (param.IsOut || param.IsIn || param.IsOptional)
        {
            ThrowArgumentExceptionForParamChecks(
                remotedInterfaceKindName,
                remotedInterfaceType,
                methodInfo,
                param,
                SR.ErrorRemotedMethodHasOutRefOptionalParameter);
        }
    }

    private static void ThrowArgumentExceptionForParamChecks(
        string remotedInterfaceKindName,
        Type remotedInterfaceType,
        MethodInfo methodInfo,
        ParameterInfo param,
        string resourceName)
    {
        throw new ArgumentException(
            string.Format(
                CultureInfo.CurrentCulture,
                resourceName,
                remotedInterfaceKindName,
                methodInfo.Name,
                remotedInterfaceType.FullName,
                param.Name),
            remotedInterfaceKindName + "InterfaceType");
    }
}