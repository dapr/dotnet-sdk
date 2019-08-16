// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Description
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Threading;
    using Microsoft.Actions.Actors.Common;

    internal class MethodDescription
    {
        private readonly bool useCRCIdGeneration;

        private MethodDescription(
            MethodInfo methodInfo,
            MethodArgumentDescription[] arguments,
            bool hasCancellationToken,
            bool useCRCIdGeneration)
        {
            this.MethodInfo = methodInfo;
            this.useCRCIdGeneration = useCRCIdGeneration;
            if (this.useCRCIdGeneration)
            {
                this.Id = IdUtil.ComputeIdWithCRC(methodInfo);
            }
            else
            {
                this.Id = IdUtil.ComputeId(methodInfo);
            }

            this.Arguments = arguments;
            this.HasCancellationToken = hasCancellationToken;
        }

        public int Id { get; }

        public string Name
        {
            get { return this.MethodInfo.Name; }
        }

        public Type ReturnType
        {
            get { return this.MethodInfo.ReturnType; }
        }

        public bool HasCancellationToken { get; }

        public MethodArgumentDescription[] Arguments { get; }

        public MethodInfo MethodInfo { get; }

        internal static MethodDescription Create(string remotedInterfaceKindName, MethodInfo methodInfo, bool useCRCIdGeneration)
        {
            var parameters = methodInfo.GetParameters();
            var argumentList = new List<MethodArgumentDescription>(parameters.Length);
            var hasCancellationToken = false;

            foreach (var param in parameters)
            {
                if (hasCancellationToken)
                {
                    // If the method has a cancellation token, then it must be the last argument.
                    throw new ArgumentException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            SR.ErrorRemotedMethodCancellationTokenOutOfOrder,
                            remotedInterfaceKindName,
                            methodInfo.Name,
                            methodInfo.DeclaringType.FullName,
                            param.Name,
                            typeof(CancellationToken)),
                        remotedInterfaceKindName + "InterfaceType");
                }

                if (param.ParameterType == typeof(CancellationToken))
                {
                    hasCancellationToken = true;
                }
                else
                {
                    argumentList.Add(MethodArgumentDescription.Create(remotedInterfaceKindName, methodInfo, param));
                }
            }

            return new MethodDescription(
                methodInfo,
                argumentList.ToArray(),
                hasCancellationToken,
                useCRCIdGeneration);
        }
    }
}
