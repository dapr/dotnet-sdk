---
type: docs
title: "Actor serialization in the .NET SDK"
linkTitle: "Actor serialization"
weight: 300000
description: Necessary steps to serialize your types using remoted Actors in .NET
---

The Dapr actor package enables you to use Dapr virtual actors within a .NET application with strongly-typed remoting, but if you intend to send and receive strongly-typed data from your methods, there are a few key ground rules to understand. In this guide, you will learn how to configure your classes and records so they are properly serialized and deserialized at runtime.

# Data Contract Serialization
When Dapr's virtual actors are invoked via the remoting proxy, your data is serialized using a serialization engine called the [Data Contract Serializer](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/serializable-types) implemented by the [DataContractSerializer](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.datacontractserializer) class, which converts your C# types to and from XML documents. When sending or receiving primitives (like strings or ints), this serialization happens transparently and there's no requisite preparation needed on your part. However, when working with complex types such as those you create, there are some important rules to take into consideration so this process works smoothly.

This serialization framework is not specific to Dapr and is separately maintained by the .NET team within the [.NET Github repository](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.DataContractSerialization/src/System/Runtime/Serialization/DataContractSerializer.cs).

## Serializable Types
There are several important considerations to keep in mind when using the Data Contract Serializer:

- By default, all types, read/write properties (after construction) and fields marked as publicly visible are serialized
- All types must either expose a public parameterless constructor or be decorated with the DataContractAttribute attribute
- Init-only setters are only supported with the use of the DataContractAttribute attribute
- Read-only fields, properties without a Get and Set method and internal or properties with private Get and Set methods are ignored during serialization
- Serialization is supported for types that use other complex types that are not themselves marked with the DataContractAttribute attribute through the use of the KnownTypesAttribute attribute
- If a type is marked with the DataContractAttribute attribute, all members you wish to serialize and deserialize must be decorated with the DataMemberAttribute attribute as well or they'll be set to their default values

## How does deserialization work?
The approach used for deserialization depends on whether or not the type is decorated with the [DataContractAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.datacontractattribute) attribute. If this attribute isn't present, an instance of the type is created using the parameterless constructor. Each of the properties and fields are then mapped into the type using their respective setters and the instance is returned to the caller. 

If the type _is_ marked with `[DataContract]`, the serializer instead uses reflection to read the metadata of the type and determine which properties or fields should be included based on whether or not they're marked with the DataMemberAttribute attribute as it's performed on an opt-in basis. It then allocates an uninitialized object in memory (avoiding the use of any constructors, parameterless or not) and then sets the value directly on each mapped property or field, even if private or uses init-only setters. Serialization callbacks are invoked as applicable throughout this process and then the object is returned to the caller. 

Use of the serialization attributes is highly recommended as they grant more flexibility to override names and namespaces and generally use more of the modern C# functionality. While the default serializer can be relied on for primitive types, it's not recommended for any of your own types, whether they be classes, structs or records.  It's recommended that if you decorate a type with the DataContractAttribute attribute, you also explicitly decorate each of the members you want to serialize or deserialize with the DataMemberAttribute attribute as well.

### .NET Classes
Classes are fully supported in the Data Contract Serializer provided that that other rules detailed on this page and the [Data Contract Serializer](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/serializable-types) documentation are also followed.

The most important thing to remember here is that you must either have a public parameterless constructor or you must decorate it with the appropriate attributes. Let's review some examples to really clarify what will and won't work.

In the following example, we present a simple class named Doodad. We don't provide an explicit constructor here, so the compiler will provide an default parameterless constructor. Because we're using [supported primitive types](###supported-primitive-types) (Guid, string and int32) and all our members have a public getter and setter, no attributes are required and we'll be able to use this class without issue when sending and receiving it from a Dapr actor method.

```csharp
public class Doodad
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; }
}
```

By default, this will serialize using the names of the members as used in the type and whatever values it was instantiated with:

```xml
<Doodad>
  <Id>a06ced64-4f42-48ad-84dd-46ae6a7e333d</Id>
  <Name>DoodadName</Name>
  <Count>5</Count>
</Doodad>
```

So let's tweak it - let's add our own constructor and only use init-only setters on the members. This will fail to serialize and deserialize not because of the use of the init-only setters, but because there's no parameterless constructors.

```csharp
// WILL NOT SERIALIZE PROPERLY!
public class Doodad
{
    public Doodad(string name, int count)
    {
        Id = Guid.NewGuid();
        Name = name;
        Count = count;
    }

    public Guid Id { get; set; }
    public string Name { get; init; }
    public int Count { get; init; }
}
```

If we add a public parameterless constructor to the type, we're good to go and this will work without further annotations. 

```csharp
public class Doodad
{
    public Doodad()
    {
    }

    public Doodad(string name, int count)
    {
        Id = Guid.NewGuid();
        Name = name;
        Count = count;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; }
}
```

But what if we don't want to add this constructor? Perhaps you don't want your developers to accidentally create an instance of this Doodad using an unintended constructor. That's where the more flexible attributes are useful. If you decorate your type with a [DataContractAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.datacontractattribute) attribute, you can drop your parameterless constructor and it will work once again.

```csharp
[DataContract]
public class Doodad
{
    public Doodad(string name, int count)
    {
        Id = Guid.NewGuid();
        Name = name;
        Count = count;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; }
}
```

In the above example, we don't need to also use the [DataMemberAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.datamemberattribute) attributes because again, we're using [built-in primitives](###supported-primitive-types) that the serializer supports. But, we do get more flexibility if we use the attributes. From the DataContractAttribute attribute, we can specify our own XML namespace with the Namespace argument and, via the Name argument, change the name of the type as used when serialized into the XML document. 

It's a recommended practice to append the DataContractAttribute attribute to the type and the DataMemberAttribute attributes to all the members you want to serialize anyway - if they're not necessary and you're not changing the default values, they'll just be ignored, but they give you a mechanism to opt into serializing members that wouldn't otherwise have been included such as those marked as private or that are themselves complex types or collections.

Note that if you do opt into serializing your private members, their values will be serialized into plain text - they can very well be viewed, intercepted and potentially manipulated based on how you're handing the data once serialized, so it's an important consideration whether you want to mark these members or not in your use case.

In the following example, we'll look at using the attributes to change the serialized names of some of the members as well as introduce the [IgnoreDataMemberAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.ignoredatamemberattribute) attribute. As the name indicates, this tells the serializer to skip this property even though it'd be otherwise eligible to serialize. Further, because I'm decorating the type with the DataContractAttribute attribute, it means that I can use init-only setters on the properties.

```csharp
[DataContract(Name="Doodad")]
public class Doodad
{
    public Doodad(string name = "MyDoodad", int count = 5)
    {
        Id = Guid.NewGuid();
        Name = name;
        Count = count;
    }

    [DataMember(Name = "id")]
    public Guid Id { get; init; }
    [IgnoreDataMember]
    public string Name { get; init; }
    [DataMember]
    public int Count { get; init; }
}
```

When this is serialized, because we're changing the names of the serialized members, we can expect a new instance of Doodad using the default values this to be serialized as:

```xml
<Doodad>
  <id>a06ced64-4f42-48ad-84dd-46ae6a7e333d</id>
  <Count>5</Count>
</Doodad>
```

#### Classes in C# 12 - Primary Constructors
C# 12 brought us primary constructors on classes. Use of a primary constructor means the compiler will be prevented from creating the default implicit parameterless constructor. While a primary constructor on a class doesn't generate any public properties, it does mean that if you pass this primary constructor any arguments or have non-primitive types in your class, you'll either need to specify your own parameterless constructor or use the serialization attributes.

Here's an example where we're using the primary constructor to inject an ILogger to a field and add our own parameterless constructor without the need for any attributes.

```csharp
public class Doodad(ILogger<Doodad> _logger)
{
    public Doodad() {} //Our parameterless constructor

    public Doodad(string name, int count)
    {
        Id = Guid.NewGuid();
        Name = name;
        Count = count;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; } 
}
```

And using our serialization attributes (again, opting for init-only setters since we're using the serialization attributes):

```csharp
[DataContract]
public class Doodad(ILogger<Doodad> _logger)
{
    public Doodad(string name, int count)
    {
        Id = Guid.NewGuid();
        Name = name;
        Count = count;
    }

    [DataMember]
    public Guid Id { get; init; }
    [DataMember]
    public string Name { get; init; }
    [DataMember]
    public int Count { get; init; }
}
```

### .NET Structs
Structs are supported by the Data Contract serializer provided that they are marked with the DataContractAttribute attribute and the members you wish to serialize are marked with the DataMemberAttribute attribute. Further, to support deserialization, the struct will also need to have a parameterless constructor. This works even if you define your own parameterless constructor as enabled in C# 10.

```csharp
[DataContract]
public struct Doodad
{
    [DataMember]
    public int Count { get; set; }
}
```

### .NET Records
Records were introduced in C# 9 and follow precisely the same rules as classes when it comes to serialization. We recommend that you should decorate all your records with the DataContractAttribute attribute and members you wish to serialize with DataMemberAttribute attributes so you don't experience any deserialization issues using this or other newer C# functionalities. Because record classes use init-only setters for properties by default and encourage the use of the primary constructor, applying these attributes to your types ensures that the serializer can properly otherwise accommodate your types as-is.

Typically records are presented as a simple one-line statement using the new primary constructor concept:

```csharp
public record Doodad(Guid Id, string Name, int Count);
```

This will throw an error encouraging the use of the serialization attributes as soon as you use it in a Dapr actor method invocation because there's no parameterless constructor available nor is it decorated with the aforementioned attributes.

Here we add an explicit parameterless constructor and it won't throw an error, but none of the values will be set during deserialization since they're created with init-only setters. Because this doesn't use the DataContractAttribute attribute or the DataMemberAttribute attribute on any members, the serializer will be unable to map the target members correctly during deserialization.
```csharp
public record Doodad(Guid Id, string Name, int Count)
{
    public Doodad() {}
}
```

This approach does without the additional constructor and instead relies on the serialization attributes. Because we mark the type with the DataContractAttribute attribute and decorate each member with its own DataMemberAttribute attribute, the serialization engine will be able to map from the XML document to our type without issue.
```csharp
[DataContract]
public record Doodad(
        [property: DataMember] Guid Id,
        [property: DataMember] string Name,
        [property: DataMember] int Count)
```

### Supported Primitive Types
There are several types built into .NET that are considered primitive and eligible for serialization without additional effort on the part of the developer:

- [Byte](https://learn.microsoft.com/en-us/dotnet/api/system.byte)
- [SByte](https://learn.microsoft.com/en-us/dotnet/api/system.sbyte)
- [Int16](https://learn.microsoft.com/en-us/dotnet/api/system.int16)
- [Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32)
- [Int64](https://learn.microsoft.com/en-us/dotnet/api/system.int64)
- [UInt16](https://learn.microsoft.com/en-us/dotnet/api/system.uint16)
- [UInt32](https://learn.microsoft.com/en-us/dotnet/api/system.uint32)
- [UInt64](https://learn.microsoft.com/en-us/dotnet/api/system.uint64)
- [Single](https://learn.microsoft.com/en-us/dotnet/api/system.single)
- [Double](https://learn.microsoft.com/en-us/dotnet/api/system.double)
- [Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean)
- [Char](https://learn.microsoft.com/en-us/dotnet/api/system.char)
- [Decimal](https://learn.microsoft.com/en-us/dotnet/api/system.decimal)
- [Object](https://learn.microsoft.com/en-us/dotnet/api/system.object)
- [String](https://learn.microsoft.com/en-us/dotnet/api/system.string)

There are additional types that aren't actually primitives but have similar built-in support:

- [DateTime](https://learn.microsoft.com/en-us/dotnet/api/system.datetime)
- [TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan)
- [Guid](https://learn.microsoft.com/en-us/dotnet/api/system.guid)
- [Uri](https://learn.microsoft.com/en-us/dotnet/api/system.uri)
- [XmlQualifiedName](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlqualifiedname)

Again, if you want to pass these types around via your actor methods, no additional consideration is necessary as they'll be serialized and deserialized without issue. Further, types that are themselves marked with the (SerializeableAttribute)[https://learn.microsoft.com/en-us/dotnet/api/system.serializableattribute] attribute will be serialized.

### Enumeration Types
Enumerations, including flag enumerations are serializable if appropriately marked. The enum members you wish to be serialized must be marked with the [EnumMemberAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.enummemberattribute) attribute in order to be serialized. Passing a custom value into the optional Value argument on this attribute will allow you to specify the value used for the member in the serialized document instead of having the serializer derive it from the name of the member.

The enum type does not require that the type be decorated with the `DataContractAttribute` attribute - only that the members you wish to serialize be decorated with the `EnumMemberAttribute` attributes.

```csharp
public enum Colors
{
    [EnumMember]
    Red,
    [EnumMember(Value="g")]
    Green,
    Blue, //Even if used by a type, this value will not be serialized as it's not decorated with the EnumMember attribute
}
```

### Collection Types
With regards to the data contact serializer, all collection types that implement the [IEnumerable](https://learn.microsoft.com/en-us/dotnet/api/system.collections.ienumerable) interface including arays and generic collections are considered collections. Those types that implement [IDictionary](https://learn.microsoft.com/en-us/dotnet/api/system.collections.idictionary) or the generic [IDictionary<TKey, TValue>](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.idictionary-2) are considered dictionary collections; all others are list collections.

Not unlike other complex types, collection types must have a parameterless constructor available. Further, they must also have a method called Add so they can be properly serialized and deserialized. The types used by these collection types must themselves be marked with the `DataContractAttribute` attribute or otherwise be serializable as described throughout this document.

### Data Contract Versioning
As the data contract serializer is only used in Dapr with respect to serializing the values in the .NET SDK to and from the Dapr actor instances via the proxy methods, there's little need to consider versioning of data contracts as the data isn't being persisted between application versions using the same serializer. For those interested in learning more about data contract versioning visit [here](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/data-contract-versioning).

### Known Types
Nesting your own complex types is easily accommodated by marking each of the types with the [DataContractAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.datacontractattribute) attribute. This informs the serializer as to how deserialization should be performed. 
But what if you're working with polymorphic types and one of your members is a base class or interface with derived classes or other implementations? Here, you'll use the [KnownTypeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.knowntypeattribute) attribute to give a hint to the serializer about how to proceed.

When you apply the [KnownTypeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.knowntypeattribute) attribute to a type, you are informing the data contract serializer about what subtypes it might encounter allowing it to properly handle the serialization and deserialization of these types, even when the actual type at runtime is different from the declared type.

```chsarp
[DataContract]
[KnownType(typeof(DerivedClass))]
public class BaseClass
{
    //Members of the base class
}

[DataContract]
public class DerivedClass : BaseClass 
{
    //Additional members of the derived class
}
```

In this example, the `BaseClass` is marked with `[KnownType(typeof(DerivedClass))]` which tells the data contract serializer that `DerivedClass` is a possible implementation of `BaseClass` that it may need to serialize or deserialize. Without this attribute, the serialize would not be aware of the `DerivedClass` when it encounters an instance of `BaseClass` that is actually of type `DerivedClass` and this could lead to a serialization exception because the serializer would not know how to handle the derived type. By specifying all possible derived types as known types, you ensure that the serializer can process the type and its members correctly.

For more information and examples about using `[KnownType]`, please refer to the [official documentation](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/data-contract-known-types).