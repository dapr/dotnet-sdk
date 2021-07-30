// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Description
{
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
}
