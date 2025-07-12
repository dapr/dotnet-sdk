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
using System.Linq;
using Dapr.Actors.Description;
using Dapr.Actors.Runtime;

internal class ActorCodeBuilder : ICodeBuilder
{
    internal static readonly InterfaceDetailsStore InterfaceDetailsStore = new InterfaceDetailsStore();
    private static readonly ICodeBuilder Instance = new ActorCodeBuilder(new ActorCodeBuilderNames("V1"));
    private static readonly object BuildLock = new object();
    private readonly MethodBodyTypesBuilder methodBodyTypesBuilder;
    private readonly MethodDispatcherBuilder<ActorMethodDispatcherBase> methodDispatcherBuilder;
    private readonly ActorProxyGeneratorBuilder proxyGeneratorBuilder;

    private readonly Dictionary<Type, MethodBodyTypesBuildResult> methodBodyTypesBuildResultMap;
    private readonly Dictionary<Type, MethodDispatcherBuildResult> methodDispatcherBuildResultMap;
    private readonly Dictionary<Type, ActorProxyGeneratorBuildResult> proxyGeneratorBuildResultMap;

    private readonly ICodeBuilderNames codeBuilderNames;

    public ActorCodeBuilder(ICodeBuilderNames codeBuilderNames)
    {
        this.codeBuilderNames = codeBuilderNames;

        this.methodBodyTypesBuildResultMap = new Dictionary<Type, MethodBodyTypesBuildResult>();
        this.methodDispatcherBuildResultMap = new Dictionary<Type, MethodDispatcherBuildResult>();
        this.proxyGeneratorBuildResultMap = new Dictionary<Type, ActorProxyGeneratorBuildResult>();

        this.methodBodyTypesBuilder = new MethodBodyTypesBuilder(this);
        this.methodDispatcherBuilder = new MethodDispatcherBuilder<ActorMethodDispatcherBase>(this);
        this.proxyGeneratorBuilder = new ActorProxyGeneratorBuilder(this);
    }

    ICodeBuilderNames ICodeBuilder.Names
    {
        get { return this.codeBuilderNames; }
    }

    public static ActorProxyGenerator GetOrCreateProxyGenerator(Type actorInterfaceType)
    {
        lock (BuildLock)
        {
            return (ActorProxyGenerator)Instance.GetOrBuildProxyGenerator(actorInterfaceType).ProxyGenerator;
        }
    }

    public static ActorMethodDispatcherBase GetOrCreateMethodDispatcher(Type actorInterfaceType)
    {
        lock (BuildLock)
        {
            return (ActorMethodDispatcherBase)Instance.GetOrBuilderMethodDispatcher(actorInterfaceType).MethodDispatcher;
        }
    }

    MethodDispatcherBuildResult ICodeBuilder.GetOrBuilderMethodDispatcher(Type interfaceType)
    {
        if (this.TryGetMethodDispatcher(interfaceType, out var result))
        {
            return result;
        }

        result = this.BuildMethodDispatcher(interfaceType);
        this.UpdateMethodDispatcherBuildMap(interfaceType, result);

        return result;
    }

    MethodBodyTypesBuildResult ICodeBuilder.GetOrBuildMethodBodyTypes(Type interfaceType)
    {
        if (this.methodBodyTypesBuildResultMap.TryGetValue(interfaceType, out var result))
        {
            return result;
        }

        result = this.BuildMethodBodyTypes(interfaceType);
        this.methodBodyTypesBuildResultMap.Add(interfaceType, result);

        return result;
    }

    ActorProxyGeneratorBuildResult ICodeBuilder.GetOrBuildProxyGenerator(Type interfaceType)
    {
        if (this.TryGetProxyGenerator(interfaceType, out var result))
        {
            return result;
        }

        result = this.BuildProxyGenerator(interfaceType);
        this.UpdateProxyGeneratorMap(interfaceType, result);

        return result;
    }

    internal static bool TryGetKnownTypes(int interfaceId, out InterfaceDetails interfaceDetails)
    {
        return InterfaceDetailsStore.TryGetKnownTypes(interfaceId, out interfaceDetails);
    }

    internal static bool TryGetKnownTypes(string interfaceName, out InterfaceDetails interfaceDetails)
    {
        return InterfaceDetailsStore.TryGetKnownTypes(interfaceName, out interfaceDetails);
    }

    protected MethodDispatcherBuildResult BuildMethodDispatcher(Type interfaceType)
    {
        var actorInterfaceDescription = ActorInterfaceDescription.CreateUsingCRCId(interfaceType);
        var res = this.methodDispatcherBuilder.Build(actorInterfaceDescription);
        return res;
    }

    protected MethodBodyTypesBuildResult BuildMethodBodyTypes(Type interfaceType)
    {
        var actorInterfaceDescriptions = ActorInterfaceDescription.CreateUsingCRCId(interfaceType);
        var result = this.methodBodyTypesBuilder.Build(actorInterfaceDescriptions);
        InterfaceDetailsStore.UpdateKnownTypeDetail(actorInterfaceDescriptions, result);
        return result;
    }

    protected ActorProxyGeneratorBuildResult BuildProxyGenerator(Type interfaceType)
    {
        // create all actor interfaces that this interface derives from
        var actorInterfaces = new List<Type>() { interfaceType };
        actorInterfaces.AddRange(interfaceType.GetActorInterfaces());

        // create interface descriptions for all interfaces
        var actorInterfaceDescriptions = actorInterfaces.Select<Type, InterfaceDescription>(
            t => ActorInterfaceDescription.CreateUsingCRCId(t));

        var res = this.proxyGeneratorBuilder.Build(interfaceType, actorInterfaceDescriptions);
        return res;
    }

    protected void UpdateMethodDispatcherBuildMap(Type interfaceType, MethodDispatcherBuildResult result)
    {
        this.methodDispatcherBuildResultMap.Add(interfaceType, result);
    }

    protected bool TryGetMethodDispatcher(
        Type interfaceType,
        out MethodDispatcherBuildResult builderMethodDispatcher)
    {
        if (this.methodDispatcherBuildResultMap.TryGetValue(interfaceType, out var result))
        {
            {
                builderMethodDispatcher = result;
                return true;
            }
        }

        builderMethodDispatcher = null;
        return false;
    }

    protected void UpdateProxyGeneratorMap(Type interfaceType, ActorProxyGeneratorBuildResult result)
    {
        this.proxyGeneratorBuildResultMap.Add(interfaceType, result);
    }

    protected bool TryGetProxyGenerator(Type interfaceType, out ActorProxyGeneratorBuildResult orBuildProxyGenerator)
    {
        if (this.proxyGeneratorBuildResultMap.TryGetValue(interfaceType, out var result))
        {
            {
                orBuildProxyGenerator = result;
                return true;
            }
        }

        orBuildProxyGenerator = null;
        return false;
    }
}