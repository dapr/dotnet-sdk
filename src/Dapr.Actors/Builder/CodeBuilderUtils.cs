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
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

internal static class CodeBuilderUtils
{
    private static readonly ConstructorInfo DcAttrCtorInfo;
    private static readonly PropertyInfo DcAttrNamePropInfo;
    private static readonly PropertyInfo DcAttrNamespacePropInfo;
    private static readonly ConstructorInfo DmAttrCtorInfo;
    private static readonly PropertyInfo DmAttrIsRequiredPropInfo;
    private static readonly object[] EmptyObjectArray = { };

    static CodeBuilderUtils()
    {
        var dcAttrType = typeof(DataContractAttribute);
        DcAttrCtorInfo = dcAttrType.GetConstructor(Type.EmptyTypes);
        DcAttrNamePropInfo = dcAttrType.GetProperty("Name");
        DcAttrNamespacePropInfo = dcAttrType.GetProperty("Namespace");

        var dmAttrType = typeof(DataMemberAttribute);
        DmAttrCtorInfo = dmAttrType.GetConstructor(Type.EmptyTypes);
        DmAttrIsRequiredPropInfo = dmAttrType.GetProperty("IsRequired");
    }

    public static AssemblyBuilder CreateAssemblyBuilder(string assemblyName)
    {
        return AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.RunAndCollect);
    }

    public static ModuleBuilder CreateModuleBuilder(
        AssemblyBuilder assemblyBuilder,
        string moduleName)
    {
        return assemblyBuilder.DefineDynamicModule(moduleName);
    }

    public static TypeBuilder CreateClassBuilder(
        ModuleBuilder moduleBuilder,
        string ns,
        string className,
        Type baseType = null)
    {
        if (!string.IsNullOrEmpty(ns))
        {
            className = string.Concat(ns, ".", className);
        }

        if (baseType != null)
        {
            return moduleBuilder.DefineType(
                className,
                TypeAttributes.Public | TypeAttributes.Class,
                baseType);
        }

        return moduleBuilder.DefineType(
            className,
            TypeAttributes.Public | TypeAttributes.Class);
    }

    public static TypeBuilder CreateClassBuilder(
        ModuleBuilder moduleBuilder,
        string ns,
        string className,
        IEnumerable<Type> interfaces = null)
    {
        return CreateClassBuilder(moduleBuilder, ns, className, null, interfaces);
    }

    public static TypeBuilder CreateClassBuilder(
        ModuleBuilder moduleBuilder,
        string ns,
        string className,
        Type baseType,
        IEnumerable<Type> interfaces)
    {
        var typeBuilder = CreateClassBuilder(moduleBuilder, ns, className, baseType);
        if (interfaces != null)
        {
            foreach (var interfaceType in interfaces)
            {
                typeBuilder.AddInterfaceImplementation(interfaceType);
            }
        }

        return typeBuilder;
    }

    public static FieldBuilder CreateFieldBuilder(TypeBuilder typeBuilder, Type fieldType, string fieldName)
    {
        return typeBuilder.DefineField(
            fieldName,
            fieldType,
            FieldAttributes.Public);
    }

    public static MethodBuilder CreatePublicMethodBuilder(
        TypeBuilder typeBuilder,
        string methodName)
    {
        return typeBuilder.DefineMethod(
            methodName,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual);
    }

    public static MethodBuilder CreatePublicMethodBuilder(
        TypeBuilder typeBuilder,
        string methodName,
        Type returnType,
        params Type[] parameterTypes)
    {
        return typeBuilder.DefineMethod(
            methodName,
            (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual),
            returnType,
            parameterTypes);
    }

    public static MethodBuilder CreateProtectedMethodBuilder(
        TypeBuilder typeBuilder,
        string methodName,
        Type returnType,
        params Type[] parameterTypes)
    {
        return typeBuilder.DefineMethod(
            methodName,
            (MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual),
            returnType,
            parameterTypes);
    }

    public static MethodBuilder CreateExplitInterfaceMethodBuilder(
        TypeBuilder typeBuilder,
        MethodInfo interfaceMethod)
    {
        var parameters = interfaceMethod.GetParameters();
        var parameterTypes = parameters.Select(pi => pi.ParameterType).ToArray();

        var methodAttrs = (MethodAttributes.Private |
                           MethodAttributes.HideBySig |
                           MethodAttributes.NewSlot |
                           MethodAttributes.Virtual |
                           MethodAttributes.Final);

        var methodBuilder = typeBuilder.DefineMethod(
            string.Concat(interfaceMethod.DeclaringType.Name, ".", interfaceMethod.Name),
            methodAttrs,
            interfaceMethod.ReturnType,
            parameterTypes);

        typeBuilder.DefineMethodOverride(methodBuilder, interfaceMethod);
        return methodBuilder;
    }

    #region Data Contract Type Generation Utilities

    public static TypeBuilder CreateDataContractTypeBuilder(
        ModuleBuilder moduleBuilder,
        string ns,
        string typeName,
        string dcNamespace = null,
        string dcName = null)
    {
        var typeBuilder = CreateClassBuilder(moduleBuilder, ns, typeName, Type.EmptyTypes);
        AddDataContractAttribute(typeBuilder, dcNamespace, dcName);
        return typeBuilder;
    }

    public static void AddDataMemberField(TypeBuilder dcTypeBuilder, Type fieldType, string fieldName)
    {
        var fieldBuilder = CreateFieldBuilder(dcTypeBuilder, fieldType, fieldName);
        fieldBuilder.SetCustomAttribute(CreateCustomDataMemberAttributeBuilder());
    }

    private static void AddDataContractAttribute(TypeBuilder typeBuilder, string dcNamespace, string dcName)
    {
        typeBuilder.SetCustomAttribute(CreateCustomDataContractAttributeBuilder(dcNamespace, dcName));
    }

    private static CustomAttributeBuilder CreateCustomDataContractAttributeBuilder(string dcNamespace, string dcName)
    {
        if (string.IsNullOrEmpty(dcName) && string.IsNullOrEmpty(dcNamespace))
        {
            return new CustomAttributeBuilder(DcAttrCtorInfo, EmptyObjectArray);
        }

        if (string.IsNullOrEmpty(dcName))
        {
            return new CustomAttributeBuilder(
                DcAttrCtorInfo,
                EmptyObjectArray,
                new[] { DcAttrNamespacePropInfo },
                new object[] { dcNamespace });
        }

        if (string.IsNullOrEmpty(dcNamespace))
        {
            return new CustomAttributeBuilder(
                DcAttrCtorInfo,
                EmptyObjectArray,
                new[] { DcAttrNamePropInfo },
                new object[] { dcName });
        }

        return new CustomAttributeBuilder(
            DcAttrCtorInfo,
            EmptyObjectArray,
            new[] { DcAttrNamespacePropInfo, DcAttrNamePropInfo },
            new object[] { dcNamespace, dcName });
    }

    private static CustomAttributeBuilder CreateCustomDataMemberAttributeBuilder()
    {
        return new CustomAttributeBuilder(
            DmAttrCtorInfo,
            EmptyObjectArray,
            new[] { DmAttrIsRequiredPropInfo },
            new object[] { false });
    }

    #endregion
}