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