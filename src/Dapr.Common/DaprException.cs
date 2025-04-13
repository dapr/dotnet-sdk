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

using System.Runtime.Serialization;

namespace Dapr;

/// <summary>
/// The base type of exceptions thrown by the Dapr .NET SDK.
/// </summary>
[Serializable]
public class DaprException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="DaprException" /> with the provided <paramref name="message" />.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public DaprException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DaprException" /> with the provided 
    /// <paramref name="message" /> and <paramref name="innerException" />.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DaprException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprException"/> class with a specified context.
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo" /> object that contains serialized object data of the exception being thrown.</param>
    /// <param name="context">The <see cref="StreamingContext" /> object that contains contextual information about the source or destination. The context parameter is reserved for future use and can be null.</param>
#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")] // add this attribute to GetObjectData
#endif
    protected DaprException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
