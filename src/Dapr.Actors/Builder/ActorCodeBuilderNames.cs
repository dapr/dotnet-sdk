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
using System.Globalization;

internal class ActorCodeBuilderNames : ICodeBuilderNames
{
    private readonly string namePrefix;

    public ActorCodeBuilderNames()
        : this("actor")
    {
    }

    public ActorCodeBuilderNames(string namePrefix)
    {
        this.namePrefix = "actor" + namePrefix;
    }

    public string InterfaceId
    {
        get { return "interfaceId"; }
    }

    public string MethodId
    {
        get { return "methodId"; }
    }

    public string RetVal
    {
        get { return "retVal"; }
    }

    public string RequestBody
    {
        get { return "requestBody"; }
    }

    public string GetMethodBodyTypesAssemblyName(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_.{1}.mt", interfaceType.FullName, this.namePrefix);
    }

    public string GetMethodBodyTypesAssemblyNamespace(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_.{1}.mt", interfaceType.FullName, this.namePrefix);
    }

    public string GetRequestBodyTypeName(string methodName)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}ReqBody", methodName);
    }

    public string GetResponseBodyTypeName(string methodName)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}RespBody", methodName);
    }

    public string GetMethodDispatcherAssemblyName(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_.{1}.disp", interfaceType.FullName, this.namePrefix);
    }

    public string GetMethodDispatcherAssemblyNamespace(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_.{1}.disp", interfaceType.FullName, this.namePrefix);
    }

    public string GetMethodDispatcherClassName(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}MethodDispatcher", interfaceType.Name);
    }

    public string GetProxyAssemblyName(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_.{1}.proxy", interfaceType.FullName, this.namePrefix);
    }

    public string GetProxyAssemblyNamespace(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_.{1}.proxy", interfaceType.FullName, this.namePrefix);
    }

    public string GetProxyClassName(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}{1}Proxy", interfaceType.Name, this.namePrefix);
    }

    public string GetProxyActivatorClassName(Type interfaceType)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}{1}ProxyActivator", interfaceType.Name, this.namePrefix);
    }

    public string GetDataContractNamespace()
    {
        return Constants.Namespace;
    }
}