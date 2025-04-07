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
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Communication;
using Dapr.Actors.Description;

internal class MethodDispatcherBuilder<TMethodDispatcher> : CodeBuilderModule
    where TMethodDispatcher : ActorMethodDispatcherBase
{
    private static readonly MethodInfo getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));
    private readonly Type methodDispatcherBaseType;
    private readonly MethodInfo continueWithResultMethodInfo;
    private readonly MethodInfo continueWithMethodInfo;
    private readonly MethodInfo checkIfitsWrapped;

    public MethodDispatcherBuilder(ICodeBuilder codeBuilder)
        : base(codeBuilder)
    {
        this.methodDispatcherBaseType = typeof(TMethodDispatcher);

        this.continueWithResultMethodInfo = this.methodDispatcherBaseType.GetMethod(
            "ContinueWithResult",
            BindingFlags.Instance | BindingFlags.NonPublic);

        this.continueWithMethodInfo = this.methodDispatcherBaseType.GetMethod(
            "ContinueWith",
            BindingFlags.Instance | BindingFlags.NonPublic);
        this.checkIfitsWrapped = this.methodDispatcherBaseType.GetMethod(
            "CheckIfItsWrappedRequest",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            CallingConventions.Any,
            new[] { typeof(IActorRequestMessageBody) },
            null);
    }

    public MethodDispatcherBuildResult Build(
        InterfaceDescription interfaceDescription)
    {
        var context = new CodeBuilderContext(
            assemblyName: this.CodeBuilder.Names.GetMethodDispatcherAssemblyName(interfaceDescription.InterfaceType),
            assemblyNamespace: this.CodeBuilder.Names.GetMethodDispatcherAssemblyNamespace(interfaceDescription
                .InterfaceType),
            enableDebugging: CodeBuilderAttribute.IsDebuggingEnabled(interfaceDescription.InterfaceType));

        var result = new MethodDispatcherBuildResult(context);

        // ensure that the method body types are built
        var methodBodyTypesBuildResult =
            this.CodeBuilder.GetOrBuildMethodBodyTypes(interfaceDescription.InterfaceType);

        // build dispatcher class
        var classBuilder = CodeBuilderUtils.CreateClassBuilder(
            context.ModuleBuilder,
            ns: context.AssemblyNamespace,
            className: this.CodeBuilder.Names.GetMethodDispatcherClassName(interfaceDescription.InterfaceType),
            baseType: this.methodDispatcherBaseType);

        this.AddCreateResponseBodyMethod(classBuilder, interfaceDescription, methodBodyTypesBuildResult);
        this.AddOnDispatchAsyncMethod(classBuilder, interfaceDescription, methodBodyTypesBuildResult);
        this.AddOnDispatchMethod(classBuilder, interfaceDescription, methodBodyTypesBuildResult);

        var methodNameMap = GetMethodNameMap(interfaceDescription);

        // create the dispatcher type, instantiate and initialize it
        result.MethodDispatcherType = classBuilder.CreateTypeInfo().AsType();
        result.MethodDispatcher = (TMethodDispatcher)Activator.CreateInstance(result.MethodDispatcherType);
        var v2MethodDispatcherBase = (ActorMethodDispatcherBase)result.MethodDispatcher;
        v2MethodDispatcherBase.Initialize(interfaceDescription, methodNameMap);

        context.Complete();
        return result;
    }

    private static void AddIfNotWrapMsgGetParameter(
        ILGenerator ilGen,
        LocalBuilder castedObject,
        MethodDescription methodDescription,
        Type requestBody)
    {
        // now invoke the method on the casted object
        ilGen.Emit(OpCodes.Ldloc, castedObject);

        if ((methodDescription.Arguments != null) && (methodDescription.Arguments.Length != 0))
        {
            var method = requestBody.GetMethod("GetParameter");
            for (var i = 0; i < methodDescription.Arguments.Length; i++)
            {
                var argument = methodDescription.Arguments[i];

                // ReSharper disable once AssignNullToNotNullAttribute
                // castedRequestBody is set to non-null in the previous if check on the same condition
                ilGen.Emit(OpCodes.Ldarg_3);
                ilGen.Emit(OpCodes.Ldc_I4, i);
                ilGen.Emit(OpCodes.Ldstr, argument.Name);
                ilGen.Emit(OpCodes.Ldtoken, argument.ArgumentType);
                ilGen.Emit(OpCodes.Call, getTypeFromHandle);
                ilGen.Emit(OpCodes.Callvirt, method);
                ilGen.Emit(OpCodes.Unbox_Any, argument.ArgumentType);
            }
        }
    }

    private static void AddIfWrapMsgGetParameters(
        ILGenerator ilGen,
        LocalBuilder castedObject,
        MethodBodyTypes methodBodyTypes)
    {
        var wrappedRequest = ilGen.DeclareLocal(typeof(object));

        var getValueMethod = typeof(WrappedMessage).GetProperty("Value").GetGetMethod();
        ilGen.Emit(OpCodes.Ldarg_3); // request object
        ilGen.Emit(OpCodes.Callvirt, getValueMethod);
        ilGen.Emit(OpCodes.Stloc, wrappedRequest);

        // then cast and  call GetField
        LocalBuilder castedRequestBody = null;

        if (methodBodyTypes.RequestBodyType != null)
        {
            // cast the request body
            // var castedRequestBody = (<RequestBodyType>)requestBody;
            castedRequestBody = ilGen.DeclareLocal(methodBodyTypes.RequestBodyType);
            ilGen.Emit(OpCodes.Ldloc, wrappedRequest); // wrapped request
            ilGen.Emit(OpCodes.Castclass, methodBodyTypes.RequestBodyType);
            ilGen.Emit(OpCodes.Stloc, castedRequestBody);
        }

        // now invoke the method on the casted object
        ilGen.Emit(OpCodes.Ldloc, castedObject);

        if (methodBodyTypes.RequestBodyType != null)
        {
            foreach (var field in methodBodyTypes.RequestBodyType.GetFields())
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                // castedRequestBody is set to non-null in the previous if check on the same condition
                ilGen.Emit(OpCodes.Ldloc, castedRequestBody);
                ilGen.Emit(OpCodes.Ldfld, field);
            }
        }
    }

    private void AddOnDispatchMethod(
        TypeBuilder classBuilder,
        InterfaceDescription interfaceDescription,
        MethodBodyTypesBuildResult methodBodyTypesBuildResult)
    {
        var dispatchMethodImpl = CodeBuilderUtils.CreateProtectedMethodBuilder(
            classBuilder,
            "OnDispatch",
            typeof(void),
            typeof(int), // methodid
            typeof(object), // remoted object
            typeof(IActorRequestMessageBody)); // requestBody

        var ilGen = dispatchMethodImpl.GetILGenerator();

        var castedObject = ilGen.DeclareLocal(interfaceDescription.InterfaceType);
        ilGen.Emit(OpCodes.Ldarg_2); // load remoted object
        ilGen.Emit(OpCodes.Castclass, interfaceDescription.InterfaceType);
        ilGen.Emit(OpCodes.Stloc, castedObject); // store casted result to local 0

        foreach (var methodDescription in interfaceDescription.Methods)
        {
            if (!TypeUtility.IsVoidType(methodDescription.ReturnType))
            {
                continue;
            }

            var elseLable = ilGen.DefineLabel();

            this.AddIfMethodIdInvokeBlock(
                ilGen: ilGen,
                elseLabel: elseLable,
                castedObject: castedObject,
                methodDescription: methodDescription,
                methodBodyTypes: methodBodyTypesBuildResult.MethodBodyTypesMap[methodDescription.Name]);

            ilGen.MarkLabel(elseLable);
        }

        ilGen.ThrowException(typeof(MissingMethodException));
    }

    private void AddIfMethodIdInvokeBlock(
        ILGenerator ilGen,
        Label elseLabel,
        LocalBuilder castedObject,
        MethodDescription methodDescription,
        MethodBodyTypes methodBodyTypes)
    {
        ilGen.Emit(OpCodes.Ldarg_1);
        ilGen.Emit(OpCodes.Ldc_I4, methodDescription.Id);
        ilGen.Emit(OpCodes.Bne_Un, elseLabel);

        // Check If its Wrapped , then call getparam
        var requestBody = typeof(IActorRequestMessageBody);

        // now invoke the method on the casted object
        ilGen.Emit(OpCodes.Ldloc, castedObject);

        // Check if its WrappedMessage
        var elseLabelforWrapped = ilGen.DefineLabel();
        this.AddACheckIfItsWrappedMessage(ilGen, elseLabelforWrapped);
        var endlabel = ilGen.DefineLabel();

        // 2 If true then call GetValue
        AddIfWrapMsgGetParameters(ilGen, castedObject, methodBodyTypes);
        ilGen.Emit(OpCodes.Br, endlabel);
        ilGen.MarkLabel(elseLabelforWrapped);

        // else call GetParameter on IServiceRemotingMessageBody
        AddIfNotWrapMsgGetParameter(ilGen, castedObject, methodDescription, requestBody);

        ilGen.MarkLabel(endlabel);

        ilGen.EmitCall(OpCodes.Callvirt, methodDescription.MethodInfo, null);
        ilGen.Emit(OpCodes.Ret);
    }

    private void AddOnDispatchAsyncMethod(
        TypeBuilder classBuilder,
        InterfaceDescription interfaceDescription,
        MethodBodyTypesBuildResult methodBodyTypesBuildResult)
    {
        var dispatchMethodImpl = CodeBuilderUtils.CreateProtectedMethodBuilder(
            classBuilder,
            "OnDispatchAsync",
            typeof(Task<IActorResponseMessageBody>),
            typeof(int), // methodid
            typeof(object), // remoted object
            typeof(IActorRequestMessageBody), // requestBody
            typeof(IActorMessageBodyFactory), // remotingmessageBodyFactory
            typeof(CancellationToken)); // CancellationToken

        var ilGen = dispatchMethodImpl.GetILGenerator();

        var castedObject = ilGen.DeclareLocal(interfaceDescription.InterfaceType);
        ilGen.Emit(OpCodes.Ldarg_2); // load remoted object
        ilGen.Emit(OpCodes.Castclass, interfaceDescription.InterfaceType);
        ilGen.Emit(OpCodes.Stloc, castedObject); // store casted result to local 0

        foreach (var methodDescription in interfaceDescription.Methods)
        {
            if (!TypeUtility.IsTaskType(methodDescription.ReturnType))
            {
                continue;
            }

            var elseLable = ilGen.DefineLabel();

            this.AddIfMethodIdInvokeAsyncBlock(
                ilGen: ilGen,
                elseLabel: elseLable,
                castedObject: castedObject,
                methodDescription: methodDescription,
                interfaceName: interfaceDescription.InterfaceType.FullName,
                methodBodyTypes: methodBodyTypesBuildResult.MethodBodyTypesMap[methodDescription.Name]);

            ilGen.MarkLabel(elseLable);
        }

        ilGen.ThrowException(typeof(MissingMethodException));
    }

    private void AddIfMethodIdInvokeAsyncBlock(
        ILGenerator ilGen,
        Label elseLabel,
        LocalBuilder castedObject,
        MethodDescription methodDescription,
        string interfaceName,
        MethodBodyTypes methodBodyTypes)
    {
        ilGen.Emit(OpCodes.Ldarg_1);
        ilGen.Emit(OpCodes.Ldc_I4, methodDescription.Id);
        ilGen.Emit(OpCodes.Bne_Un, elseLabel);

        var invokeTask = ilGen.DeclareLocal(methodDescription.ReturnType);
        var requestBody = typeof(IActorRequestMessageBody);

        // now invoke the method on the casted object
        ilGen.Emit(OpCodes.Ldloc, castedObject);

        // Check if its WrappedMessage
        var elseLabelforWrapped = ilGen.DefineLabel();
        this.AddACheckIfItsWrappedMessage(ilGen, elseLabelforWrapped);
        var endlabel = ilGen.DefineLabel();

        // 2 If true then call GetValue
        AddIfWrapMsgGetParameters(ilGen, castedObject, methodBodyTypes);
        ilGen.Emit(OpCodes.Br, endlabel);
        ilGen.MarkLabel(elseLabelforWrapped);

        // else call GetParameter on IServiceRemotingMessageBody
        AddIfNotWrapMsgGetParameter(ilGen, castedObject, methodDescription, requestBody);

        ilGen.MarkLabel(endlabel);

        if (methodDescription.HasCancellationToken)
        {
            ilGen.Emit(OpCodes.Ldarg, 5);
        }

        ilGen.EmitCall(OpCodes.Callvirt, methodDescription.MethodInfo, null);
        ilGen.Emit(OpCodes.Stloc, invokeTask);

        // call the base method to return continuation task
        if (TypeUtility.IsTaskType(methodDescription.ReturnType) &&
            methodDescription.ReturnType.GetTypeInfo().IsGenericType)
        {
            // the return is Task<IServiceRemotingMessageBody>
            var continueWithGenericMethodInfo =
                this.continueWithResultMethodInfo.MakeGenericMethod(methodDescription.ReturnType
                    .GenericTypeArguments[0]);

            ilGen.Emit(OpCodes.Ldarg_0); // base
            ilGen.Emit(OpCodes.Ldstr, interfaceName);
            ilGen.Emit(OpCodes.Ldstr, methodDescription.Name);
            ilGen.Emit(OpCodes.Ldc_I4, methodDescription.Id);
            ilGen.Emit(OpCodes.Ldarg, 4); // message body factory
            ilGen.Emit(OpCodes.Ldloc, invokeTask);
            ilGen.EmitCall(OpCodes.Call, continueWithGenericMethodInfo, null);
            ilGen.Emit(OpCodes.Ret);
        }
        else
        {
            ilGen.Emit(OpCodes.Ldarg_0); // base
            ilGen.Emit(OpCodes.Ldloc, invokeTask);
            ilGen.EmitCall(OpCodes.Call, this.continueWithMethodInfo, null);
            ilGen.Emit(OpCodes.Ret);
        }
    }

    private void AddACheckIfItsWrappedMessage(
        ILGenerator ilGen, Label elseLabelforWrapped)
    {
        var boolres = ilGen.DeclareLocal(typeof(bool));
        ilGen.Emit(OpCodes.Ldarg_3); // request object
        ilGen.Emit(OpCodes.Call, this.checkIfitsWrapped);
        ilGen.Emit(OpCodes.Stloc, boolres);
        ilGen.Emit(OpCodes.Ldloc, boolres);
        ilGen.Emit(OpCodes.Brfalse, elseLabelforWrapped);
    }

    private void AddCreateResponseBodyMethod(
        TypeBuilder classBuilder,
        InterfaceDescription interfaceDescription,
        MethodBodyTypesBuildResult methodBodyTypesBuildResult)
    {
        var methodBuilder = CodeBuilderUtils.CreateProtectedMethodBuilder(
            classBuilder,
            "CreateWrappedResponseBody",
            typeof(object), // responseBody - return value
            typeof(int), // methodId
            typeof(object)); // retval from the invoked method on the remoted object

        var ilGen = methodBuilder.GetILGenerator();

        foreach (var methodDescription in interfaceDescription.Methods)
        {
            var methodBodyTypes = methodBodyTypesBuildResult.MethodBodyTypesMap[methodDescription.Name];
            if (methodBodyTypes.ResponseBodyType == null)
            {
                continue;
            }

            var elseLabel = ilGen.DefineLabel();

            this.AddIfMethodIdCreateResponseBlock(
                ilGen,
                elseLabel,
                methodDescription.Id,
                methodBodyTypes.ResponseBodyType);

            ilGen.MarkLabel(elseLabel);
        }

        // return null; (if method id's do not match)
        ilGen.Emit(OpCodes.Ldnull);
        ilGen.Emit(OpCodes.Ret);
    }

    private void AddIfMethodIdCreateResponseBlock(
        ILGenerator ilGen,
        Label elseLabel,
        int methodId,
        Type responseType)
    {
        // if (methodId == <methodid>)
        ilGen.Emit(OpCodes.Ldarg_1);
        ilGen.Emit(OpCodes.Ldc_I4, methodId);
        ilGen.Emit(OpCodes.Bne_Un, elseLabel);

        var ctorInfo = responseType.GetConstructor(Type.EmptyTypes);
        if (ctorInfo != null)
        {
            var localBuilder = ilGen.DeclareLocal(responseType);

            // new <ResponseBodyType>
            ilGen.Emit(OpCodes.Newobj, ctorInfo);
            ilGen.Emit(OpCodes.Stloc, localBuilder);
            ilGen.Emit(OpCodes.Ldloc, localBuilder);

            // responseBody.retval = (<retvaltype>)retval;
            var fInfo = responseType.GetField(this.CodeBuilder.Names.RetVal);
            ilGen.Emit(OpCodes.Ldarg_2);
            ilGen.Emit(
                fInfo.FieldType.GetTypeInfo().IsClass ? OpCodes.Castclass : OpCodes.Unbox_Any,
                fInfo.FieldType);
            ilGen.Emit(OpCodes.Stfld, fInfo);
            ilGen.Emit(OpCodes.Ldloc, localBuilder);
            ilGen.Emit(OpCodes.Ret);
        }
        else
        {
            ilGen.Emit(OpCodes.Ldnull);
            ilGen.Emit(OpCodes.Ret);
        }
    }
}