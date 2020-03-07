// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// 
    /// </summary>
    public class ETag : IEquatable<ETag>
    {
        /// <summary>
        /// /// Initializes a new instance of the <see cref="ETag"/>
        /// </summary>
        /// <param name="etag">String for etag.</param>
        public ETag(string etag)
        {
            this.Value = etag;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ETag"/> class by using the value represented by the specified string.
        /// </summary>
        /// <param name="etag">A string for the etag</param>
        /// <value>New instance of the <see cref="ETag"/> class.</value>
        public static implicit operator ETag(string etag) => new ETag(etag);

        /// <summary>
        /// Gets the value of ETag.
        /// </summary>
        public string Value { get; }

        /// <inheritdoc/>
        public bool Equals([AllowNull] ETag other)
        {
            return this.Value.Equals(other.Value);
        }
    }
}
