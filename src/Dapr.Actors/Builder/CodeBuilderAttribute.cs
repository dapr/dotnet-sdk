// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System;
    using System.Reflection;

    /// <summary>
    /// The Attribute class to configure dyanamic code generation process for service remoting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface)]
    public class CodeBuilderAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeBuilderAttribute"/> class.
        /// </summary>
        public CodeBuilderAttribute()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether to enable debugging flag for the attribute to be used by auto code generation.
        /// </summary>
        /// <value><see cref="bool"/> to get or set enable debugging flag for the attribute to be used by auto code generation.</value>
        public bool EnableDebugging { get; set; }

        internal static bool IsDebuggingEnabled(Type type = null)
        {
            var enableDebugging = false;
            var entryAssembly = Assembly.GetEntryAssembly();

            if (entryAssembly != null)
            {
                var attribute = entryAssembly.GetCustomAttribute<CodeBuilderAttribute>();
                enableDebugging = ((attribute != null) && (attribute.EnableDebugging));
            }

            if (!enableDebugging && (type != null))
            {
                var attribute = type.GetTypeInfo().Assembly.GetCustomAttribute<CodeBuilderAttribute>();
                enableDebugging = ((attribute != null) && (attribute.EnableDebugging));

                if (!enableDebugging)
                {
                    attribute = type.GetTypeInfo().GetCustomAttribute<CodeBuilderAttribute>(true);
                    enableDebugging = ((attribute != null) && (attribute.EnableDebugging));
                }
            }

            return enableDebugging;
        }
    }
}
