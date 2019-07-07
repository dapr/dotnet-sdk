// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;

    /// <summary>
    /// A utility class to perform argument validations. 
    /// </summary>
    internal static class ArgumentVerifier
    {
        /// <summary>
        /// Throws ArgumentNullException if argument is null.
        /// </summary>
        /// <param name="value">Argument value to check.</param>
        /// <param name="name">Name of Argument.</param>
        public static void ThrowIfNull(this object value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
