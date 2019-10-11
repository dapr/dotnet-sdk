// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
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
}
