---
type: docs
title: "Actor serialization in the .NET SDK"
linkTitle: "Actor serialization"
weight: 300000
description: Necessary steps to serialize your types remoted and non-remoted Actors in .NET
---
# Actor Serialization

The Dapr actor package enables you to use Dapr virtual actors within a .NET application with either a weakly- or strongly-typed client. Each utilizes a different serialization approach. This document will review the differences and convey a few key ground rules to understand in either scenario.

Please be advised that it is not a supported scenario to use the weakly- or strongly typed actor clients interchangeably because of these different serialization approaches. The data persisted using one Actor client will not be accessible using the other Actor client, so it is important to pick one and use it consistently throughout your application.

## Weakly-typed Dapr Actor client
In this section, you will learn how to configure your C# types so they are properly serialized and deserialized at runtime when using a weakly-typed actor client. These clients use string-based names of methods with request and response payloads that are serialized using the System.Text.Json serializer. Please note that this serialization framework is not specific to Dapr and is separately maintained by the .NET team within the [.NET GitHub repository](https://github.com/dotnet/runtime/tree/main/src/libraries/System.Text.Json). 

When using the weakly-typed Dapr Actor client to invoke methods from your various actors, it's not necessary to independently serialize or deserialize the method payloads as this will happen transparently on your behalf by the SDK.

The client will use the latest version of System.Text.Json available for the version of .NET you're building against and serialization is subject to all the inherent capabilities provided in the [associated .NET documentation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview).

The serializer will be configured to use the `JsonSerializerOptions.Web` [default options](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options?pivots=dotnet-8-0#web-defaults-for-jsonserializeroptions) unless overridden with a custom options configuration which means the following are applied:
- Deserialization of the property name is performed in a case-insensitive manner
- Serialization of the property name is performed using [camel casing](https://en.wikipedia.org/wiki/Camel_case) unless the property is overridden with a `[JsonPropertyName]` attribute
- Deserialization will read numeric values from number and/or string values 

### Basic Serialization
In the following example, we present a simple class named Doodad though it could just as well be a record as well. 

```csharp
public class Doodad
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; }
}
```

By default, this will serialize using the names of the members as used in the type and whatever values it was instantiated with:

```json
{"id": "a06ced64-4f42-48ad-84dd-46ae6a7e333d", "name": "DoodadName", "count": 5}
```

### Override Serialized Property Name
The default property names can be overridden by applying the `[JsonPropertyName]` attribute to desired properties.

Generally, this isn't going to be necessary for types you're persisting to the actor state as you're not intended to read or write them independent of Dapr-associated functionality, but
the following is provided just to clearly illustrate that it's possible.

#### Override Property Names on Classes
Here's an example demonstrating the use of `JsonPropertyName` to change the name for the first property following serialization. Note that the last usage of `JsonPropertyName` on the `Count` property 
matches what it would be expected to serialize to. This is largely just to demonstrate that applying this attribute won't negatively impact anything - in fact, it might be preferable if you later 
decide to change the default serialization options but still need to consistently access the properties previously serialized before that change as `JsonPropertyName` will override those options.

```csharp
public class Doodad
{
    [JsonPropertyName("identifier")]
    public Guid Id { get; set; }
    public string Name { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
```

This would serialize to the following:

```json
{"identifier": "a06ced64-4f42-48ad-84dd-46ae6a7e333d", "name": "DoodadName", "count": 5}
```

#### Override Property Names on Records
Let's try doing the same thing with a record from C# 12 or later:

```csharp
public record Thingy(string Name, [JsonPropertyName("count")] int Count); 
```

Because the argument passed in a primary constructor (introduced in C# 12) can be applied to either a property or field within a record, using the `[JsonPropertyName]` attribute may
require specifying that you intend the attribute to apply to a property and not a field in some ambiguous cases. Should this be necessary, you'd indicate as much in the primary constructor with:

```csharp
public record Thingy(string Name, [property: JsonPropertyName("count")] int Count);
```

If `[property: ]` is applied to the `[JsonPropertyName]` attribute where it's not necessary, it will not negatively impact serialization or deserialization as the operation will
proceed normally as though it were a property (as it typically would if not marked as such).

### Enumeration types
Enumerations, including flat enumerations are serializable to JSON, but the value persisted may surprise you. Again, it's not expected that the developer should ever engage
with the serialized data independently of Dapr, but the following information may at least help in diagnosing why a seemingly mild version migration isn't working as expected.

Take the following `enum` type providing the various seasons in the year:

```csharp
public enum Season
{
    Spring,
    Summer,
    Fall,
    Winter
}
```

We'll go ahead and use a separate demonstration type that references our `Season` and simultaneously illustrate how this works with records:

```csharp
public record Engagement(string Name, Season TimeOfYear);
```

Given the following initialized instance:

```csharp
var myEngagement = new Engagement("Ski Trip", Season.Winter);
```

This would serialize to the following JSON:
```json
{"name":  "Ski Trip", "season":  3}
```

That might be unexpected that our `Season.Winter` value was represented as a `3`, but this is because the serializer is going to automatically use numeric representations
of the enum values starting with zero for the first value and incrementing the numeric value for each additional value available. Again, if a migration were taking place and
a developer had flipped the order of the enums, this would affect a breaking change in your solution as the serialized numeric values would point to different values when deserialized.

Rather, there is a `JsonConverter` available with `System.Text.Json` that will instead opt to use a string-based value instead of the numeric value. The `[JsonConverter]` attribute needs
to be applied to be enum type itself to enable this, but will then be realized in any downstream serialization or deserialization operation that references the enum.

```csharp
[JsonConverter(typeof(JsonStringEnumConverter<Season>))]
public enum Season
{
    Spring,
    Summer,
    Fall,
    Winter
}
```

Using the same values from our `myEngagement` instance above, this would produce the following JSON instead:

```json
{"name":  "Ski Trip", "season":  "Winter"}
```

As a result, the enum members can be shifted around without fear of introducing errors during deserialization.

#### Custom Enumeration Values

The System.Text.Json serialization platform doesn't, out of the box, support the use of `[EnumMember]` to allow you to change the value of enum that's used during serialization or deserialization, but
there are scenarios where this could be useful. Again, assume that you're tasking with refactoring the solution to apply some better names to your various
enums. You're using the `JsonStringEnumConverter<TType>` detailed above so you're saving the name of the enum to value instead of a numeric value, but if you change
the enum name, that will introduce a breaking change as the name will no longer match what's in state. 

Do note that if you opt into using this approach, you should decorate all your enum members with the `[EnumMeber]` attribute so that the values are consistently applied for each enum value instead
of haphazardly. Nothing will validate this at build or runtime, but it is considered a best practice operation.

How can you specify the precise value persisted while still changing the name of the enum member in this scenario? Use a custom `JsonConverter` with an extension method that can pull the value
out of the attached `[EnumMember]` attributes where provided. Add the following to your solution:

```csharp
public sealed class EnumMemberJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    /// <summary>Reads and converts the JSON to type <typeparamref name="T" />.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Get the string value from the JSON reader
        var value = reader.GetString();

        // Loop through all the enum values
        foreach (var enumValue in Enum.GetValues<T>())
        {
            // Get the value from the EnumMember attribute, if any
            var enumMemberValue = GetValueFromEnumMember(enumValue);

            // If the values match, return the enum value
            if (value == enumMemberValue)
            {
                return enumValue;
            }
        }

        // If no match found, throw an exception
        throw new JsonException($"Invalid value for {typeToConvert.Name}: {value}");
    }

    /// <summary>Writes a specified value as JSON.</summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Get the value from the EnumMember attribute, if any
        var enumMemberValue = GetValueFromEnumMember(value);

        // Write the value to the JSON writer
        writer.WriteStringValue(enumMemberValue);
    }

    private static string GetValueFromEnumMember(T value)
    {
        MemberInfo[] member = typeof(T).GetMember(value.ToString(), BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
        if (member.Length == 0)
            return value.ToString();
        object[] customAttributes = member.GetCustomAttributes(typeof(EnumMemberAttribute), false);
        if (customAttributes.Length != 0)
        {
            EnumMemberAttribute enumMemberAttribute = (EnumMemberAttribute)customAttributes;
            if (enumMemberAttribute != null && enumMemberAttribute.Value != null)
                return enumMemberAttribute.Value;
        }
        return value.ToString();
    }
}
```

Now let's add a sample enumerator. We'll set a value that uses the lower-case version of each enum member to demonstrate this. Don't forget to decorate the enum with the `JsonConverter`
attribute and reference our custom converter in place of the numeral-to-string converter used in the last section.

```csharp
[JsonConverter(typeof(EnumMemberJsonConverter<Season>))]
public enum Season
{
    [EnumMember(Value="spring")]
    Spring,
    [EnumMember(Value="summer")]
    Summer,
    [EnumMember(Value="fall")]
    Fall,
    [EnumMember(Value="winter")]
    Winter
}
```

Let's use our sample record from before. We'll also add a `[JsonPropertyName]` attribute just to augment the demonstration:
```csharp
public record Engagement([property: JsonPropertyName("event")] string Name, Season TimeOfYear);
```

And finally, let's initialize a new instance of this:

```csharp
var myEngagement = new Engagement("Conference", Season.Fall);
```

This time, serialization will take into account the values from the attached `[EnumMember]` attribute providing us a mechanism to refactor our application without necessitating 
a complex versioning scheme for our existing enum values in the state.

```json
{"event":  "Conference",  "season":  "fall"}
```

### Polymorphic Serialization
When working with polymorphic types in Dapr Actor clients, it is essential to handle serialization and deserialization correctly to ensure that the appropriate
derived types are instantiated. Polymorphic serialization allows you to serialize objects of a base type while preserving the specific derived type information.

To enable polymorphic deserialization, you must use the `[JsonPolymorphic]` attribute on your base type. Additionally,
it is crucial to include the `[AllowOutOfOrderMetadataProperties]` attribute to ensure that metadata properties, such as `$type`
can be processed correctly by System.Text.Json even if they are not the first properties in the JSON object.

#### Example
```cs
[JsonPolymorphic]
[AllowOutOfOrderMetadataProperties]
public abstract class SampleValueBase
{
    public string CommonProperty { get; set; }
}

public class DerivedSampleValue : SampleValueBase
{
    public string SpecificProperty { get; set; }
}
```
In this example, the `SampleValueBase` class is marked with both `[JsonPolymorphic]` and `[AllowOutOfOrderMetadataProperties]` 
attributes. This setup ensures that the `$type` metadata property can be correctly identified and processed during 
deserialization, regardless of its position in the JSON object.

By following this approach, you can effectively manage polymorphic serialization and deserialization in your Dapr Actor
clients, ensuring that the correct derived types are instantiated and used.


## Strongly-typed Dapr Actor client
In this section, you will learn how to configure your classes and records so they are properly serialized and deserialized at runtime when using a strongly-typed actor client. These clients are implemented using .NET interfaces and are <u>not</u> compatible with Dapr Actors written using other languages.

This actor client serializes data using an engine called the [Data Contract Serializer](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/serializable-types) which converts your C# types to and from XML documents. This serialization framework is not specific to Dapr and is separately maintained by the .NET team within the [.NET GitHub repository](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.DataContractSerialization/src/System/Runtime/Serialization/DataContractSerializer.cs).

When sending or receiving primitives (like strings or ints), this serialization happens transparently and there's no requisite preparation needed on your part. However, when working with complex types such as those you create, there are some important rules to take into consideration so this process works smoothly.

### Serializable Types
There are several important considerations to keep in mind when using the Data Contract Serializer:

- By default, all types, read/write properties (after construction) and fields marked as publicly visible are serialized
- All types must either expose a public parameterless constructor or be decorated with the DataContractAttribute attribute
- Init-only setters are only supported with the use of the DataContractAttribute attribute
- Read-only fields, properties without a Get and Set method and internal or properties with private Get and Set methods are ignored during serialization
- Serialization is supported for types that use other complex types that are not themselves marked with the DataContractAttribute attribute through the use of the KnownTypesAttribute attribute
- If a type is marked with the DataContractAttribute attribute, all members you wish to serialize and deserialize must be decorated with the DataMemberAttribute attribute as well or they'll be set to their default values

### How does deserialization work?
The approach used for deserialization depends on whether or not the type is decorated with the [DataContractAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.datacontractattribute) attribute. If this attribute isn't present, an instance of the type is created using the parameterless constructor. Each of the properties and fields are then mapped into the type using their respective setters and the instance is returned to the caller. 

If the type _is_ marked with `[DataContract]`, the serializer instead uses reflection to read the metadata of the type and determine which properties or fields should be included based on whether or not they're marked with the DataMemberAttribute attribute as it's performed on an opt-in basis. It then allocates an uninitialized object in memory (avoiding the use of any constructors, parameterless or not) and then sets the value directly on each mapped property or field, even if private or uses init-only setters. Serialization callbacks are invoked as applicable throughout this process and then the object is returned to the caller. 

Use of the serialization attributes is highly recommended as they grant more flexibility to override names and namespaces and generally use more of the modern C# functionality. While the default serializer can be relied on for primitive types, it's not recommended for any of your own types, whether they be classes, structs or records.  It's recommended that if you decorate a type with the DataContractAttribute attribute, you also explicitly decorate each of the members you want to serialize or deserialize with the DataMemberAttribute attribute as well.

#### .NET Classes
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

##### Classes in C# 12 - Primary Constructors
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

#### .NET Structs
Structs are supported by the Data Contract serializer provided that they are marked with the DataContractAttribute attribute and the members you wish to serialize are marked with the DataMemberAttribute attribute. Further, to support deserialization, the struct will also need to have a parameterless constructor. This works even if you define your own parameterless constructor as enabled in C# 10.

```csharp
[DataContract]
public struct Doodad
{
    [DataMember]
    public int Count { get; set; }
}
```

#### .NET Records
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

#### Supported Primitive Types
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

#### Enumeration Types
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

#### Collection Types
With regards to the data contact serializer, all collection types that implement the [IEnumerable](https://learn.microsoft.com/en-us/dotnet/api/system.collections.ienumerable) interface including arays and generic collections are considered collections. Those types that implement [IDictionary](https://learn.microsoft.com/en-us/dotnet/api/system.collections.idictionary) or the generic [IDictionary<TKey, TValue>](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.idictionary-2) are considered dictionary collections; all others are list collections.

Not unlike other complex types, collection types must have a parameterless constructor available. Further, they must also have a method called Add so they can be properly serialized and deserialized. The types used by these collection types must themselves be marked with the `DataContractAttribute` attribute or otherwise be serializable as described throughout this document.

#### Data Contract Versioning
As the data contract serializer is only used in Dapr with respect to serializing the values in the .NET SDK to and from the Dapr actor instances via the proxy methods, there's little need to consider versioning of data contracts as the data isn't being persisted between application versions using the same serializer. For those interested in learning more about data contract versioning visit [here](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/data-contract-versioning).

#### Known Types
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