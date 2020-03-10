// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------


namespace Dapr.Client
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
        public static void ThrowIfNull(object value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        /// <summary>
        /// Validates string and throws:
        /// ArgumentNullException if argument is null.
        /// ArgumentException if argument is empty.
        /// </summary>
        /// <param name="value">Argument value to check.</param>
        /// <param name="name">Name of Argument.</param>
        public static void ThrowIfNullOrEmpty(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("The value cannot be null or empty", nameof(name));
            }
        }
    }
}
