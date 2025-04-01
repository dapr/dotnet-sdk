﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Dapr.Actors.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class SR {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal SR() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Dapr.Actors.Resources.SR", typeof(SR).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The actor state name &apos;{0}&apos; already exist..
        /// </summary>
        internal static string ActorStateAlreadyExists {
            get {
                return ResourceManager.GetString("ActorStateAlreadyExists", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid Client for remoting..
        /// </summary>
        internal static string Error_InvalidOperation {
            get {
                return ResourceManager.GetString("Error_InvalidOperation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CallBack Channel Not Found for this ClientId  : &apos;{0}&apos;.
        /// </summary>
        internal static string ErrorClientCallbackChannelNotFound {
            get {
                return ResourceManager.GetString("ErrorClientCallbackChannelNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to deserialize and get remote exception  {0}.
        /// </summary>
        internal static string ErrorDeserializationFailure {
            get {
                return ResourceManager.GetString("ErrorDeserializationFailure", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0} is not an actor events interface. The actor event interface must only derive from &apos;{1}&apos;..
        /// </summary>
        internal static string ErrorEventInterfaceMustBeIActorEvents {
            get {
                return ResourceManager.GetString("ErrorEventInterfaceMustBeIActorEvents", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The exception {0} was unhandled on the service and could not be serialized for transferring to the client..
        /// </summary>
        internal static string ErrorExceptionSerializationFailed1 {
            get {
                return ResourceManager.GetString("ErrorExceptionSerializationFailed1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Detailed Remote Exception Information: {0}.
        /// </summary>
        internal static string ErrorExceptionSerializationFailed2 {
            get {
                return ResourceManager.GetString("ErrorExceptionSerializationFailed2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Header with name &apos;{0}&apos; already exists.
        /// </summary>
        internal static string ErrorHeaderAlreadyExists {
            get {
                return ResourceManager.GetString("ErrorHeaderAlreadyExists", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Interface id &apos;{0}&apos; is not implemented by object &apos;{1}&apos;.
        /// </summary>
        internal static string ErrorInterfaceNotImplemented {
            get {
                return ResourceManager.GetString("ErrorInterfaceNotImplemented", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Client is trying to connect to invalid address {0}..
        /// </summary>
        internal static string ErrorInvalidAddress {
            get {
                return ResourceManager.GetString("ErrorInvalidAddress", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The requested resource/content/path does not exist on the server..
        /// </summary>
        internal static string ErrorMessageHTTP404 {
            get {
                return ResourceManager.GetString("ErrorMessageHTTP404", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No MethodDispatcher is found for interface id &apos;{0}&apos;.
        /// </summary>
        internal static string ErrorMethodDispatcherNotFound {
            get {
                return ResourceManager.GetString("ErrorMethodDispatcherNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The object of type &apos;{0}&apos; does support the method &apos;{1}&apos;.
        /// </summary>
        internal static string ErrorMethodNotImplemented {
            get {
                return ResourceManager.GetString("ErrorMethodNotImplemented", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{0}&apos; of interface &apos;{1}&apos; is not supported in remoting V1..
        /// </summary>
        internal static string ErrorMethodNotSupportedInRemotingV1 {
            get {
                return ResourceManager.GetString("ErrorMethodNotSupportedInRemotingV1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method Id &apos;{0}&apos; for interface Id &apos;{1}&apos; not found in service implementation. If a new method is added to interface and client &amp; service are being upgraded at the same time, its possible that client got upgraded before the service. If a method is removed from the interface and client &amp; service are being upgraded at the same time, its possible that service got upgraded before the client. Addition or removal of methods to an interface should be performed as a phased upgrade..
        /// </summary>
        internal static string ErrorMissingMethod {
            get {
                return ResourceManager.GetString("ErrorMissingMethod", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Actor State with name {0} was not found..
        /// </summary>
        internal static string ErrorNamedActorStateNotFound {
            get {
                return ResourceManager.GetString("ErrorNamedActorStateNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The actor type &apos;{0}&apos; does not implement any actor interfaces or one of the interfaces implemented is not an actor interface. All interfaces(including its parent interface) implemented by actor type must be actor interface. An actor interface is the one that ultimately derives from &apos;{1}&apos; type..
        /// </summary>
        internal static string ErrorNoActorInterfaceFound {
            get {
                return ResourceManager.GetString("ErrorNoActorInterfaceFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The service type &apos;{0}&apos; does not implement any service interfaces or one of the interfaces implemented is not a service interface. All interfaces(including its parent interface) implemented by service type must be service interface. A service interface is the one that ultimately derives from &apos;{1}&apos; type..
        /// </summary>
        internal static string ErrorNoServiceInterfaceFound {
            get {
                return ResourceManager.GetString("ErrorNoServiceInterfaceFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not an Actor. An actor type must derive from &apos;{1}&apos;..
        /// </summary>
        internal static string ErrorNotAnActor {
            get {
                return ResourceManager.GetString("ErrorNotAnActor", resourceCulture);
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not an Actor Interface. An actor type must derive from &apos;{1}&apos;..
        /// </summary>
        internal static string ErrorNotAnActorInterface
        {
            get
            {
                return ResourceManager.GetString("ErrorNotAnActorInterface", resourceCulture);
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not an actor interface as it does not derive from the interface &apos;{1}&apos;..
        /// </summary>
        internal static string ErrorNotAnActorInterface_DerivationCheck1 {
            get {
                return ResourceManager.GetString("ErrorNotAnActorInterface_DerivationCheck1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not an actor interface as it derive from a non actor interface &apos;{1}&apos;. All actor interfaces must derive from &apos;{2}&apos;..
        /// </summary>
        internal static string ErrorNotAnActorInterface_DerivationCheck2 {
            get {
                return ResourceManager.GetString("ErrorNotAnActorInterface_DerivationCheck2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not an Actor interface as it is not an interface..
        /// </summary>
        internal static string ErrorNotAnActorInterface_InterfaceCheck {
            get {
                return ResourceManager.GetString("ErrorNotAnActorInterface_InterfaceCheck", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not an service interface as it does not derive from the interface &apos;{1}&apos;..
        /// </summary>
        internal static string ErrorNotAServiceInterface_DerivationCheck1 {
            get {
                return ResourceManager.GetString("ErrorNotAServiceInterface_DerivationCheck1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not an service interface as it derive from a non service interface &apos;{1}&apos;. All service interfaces must derive from &apos;{2}&apos;..
        /// </summary>
        internal static string ErrorNotAServiceInterface_DerivationCheck2 {
            get {
                return ResourceManager.GetString("ErrorNotAServiceInterface_DerivationCheck2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type &apos;{0}&apos; is not a service interface as it is not an interface. .
        /// </summary>
        internal static string ErrorNotAServiceInterface_InterfaceCheck {
            get {
                return ResourceManager.GetString("ErrorNotAServiceInterface_InterfaceCheck", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The  {0} interface &apos;{1}&apos; is using generics. Generic interfaces cannot be remoted..
        /// </summary>
        internal static string ErrorRemotedInterfaceIsGeneric {
            get {
                return ResourceManager.GetString("ErrorRemotedInterfaceIsGeneric", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; has &apos;{4}&apos; parameter &apos;{3}&apos;, and it is not the last parameter. If a method of the {0} interface has parameter of type &apos;{4}&apos; it must be the last parameter..
        /// </summary>
        internal static string ErrorRemotedMethodCancellationTokenOutOfOrder {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodCancellationTokenOutOfOrder", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; does not return Task or Task&lt;&gt;. The {0} interface methods must be async and must return either Task or Task&lt;&gt;..
        /// </summary>
        internal static string ErrorRemotedMethodDoesNotReturnTask {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodDoesNotReturnTask", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; returns &apos;{3}&apos;. The {0} interface methods must have a return of type &apos;{4}&apos;..
        /// </summary>
        internal static string ErrorRemotedMethodDoesNotReturnVoid {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodDoesNotReturnVoid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; is using generics. The {0} interface methods cannot use generics..
        /// </summary>
        internal static string ErrorRemotedMethodHasGenerics {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodHasGenerics", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; has out/ref/optional parameter &apos;{3}&apos;. The {0} interface methods must not have out, ref or optional parameters..
        /// </summary>
        internal static string ErrorRemotedMethodHasOutRefOptionalParameter {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodHasOutRefOptionalParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; has variable length parameter &apos;{3}&apos;. The {0} interface methods must not have variable length parameters..
        /// </summary>
        internal static string ErrorRemotedMethodHasVarArgParameter {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodHasVarArgParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; is using a variable argument list. The {0} interface methods cannot have a variable argument list..
        /// </summary>
        internal static string ErrorRemotedMethodHasVarArgs {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodHasVarArgs", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{1}&apos; of {0} interface &apos;{2}&apos; is overloaded. The {0} interface methods cannot be overloaded..
        /// </summary>
        internal static string ErrorRemotedMethodsIsOverloaded {
            get {
                return ResourceManager.GetString("ErrorRemotedMethodsIsOverloaded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The method &apos;{0}&apos; is not valid for &apos;{1}&apos; ActorId..
        /// </summary>
        internal static string InvalidActorKind {
            get {
                return ResourceManager.GetString("InvalidActorKind", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Server returned error while processing the request, but did not provide a meaningful error response. Response Error Code {0}.
        /// </summary>
        internal static string ServerErrorNoMeaningFulResponse {
            get {
                return ResourceManager.GetString("ServerErrorNoMeaningFulResponse", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TimeSpan TotalMilliseconds specified value must be between {0} and {1} .
        /// </summary>
        internal static string TimerArgumentOutOfRange {
            get {
                return ResourceManager.GetString("TimerArgumentOutOfRange", resourceCulture);
            }
        }

        /// <summary>
        ///   Looks up a localized string stating repetitions specified value must be a valid positive integer.
        /// </summary>
        internal static string RepetitionsArgumentOutOfRange
        {
            get {
                return ResourceManager.GetString("RepetitionsArgumentOutOfRange", resourceCulture);
            }
        }
    }
}
