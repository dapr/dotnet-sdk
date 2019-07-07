// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System.Globalization;

    internal sealed class ActorTrace
    {
        internal static readonly ActorTrace Instance = new ActorTrace();

        /// <summary>
        /// Prevents a default instance of the <see cref="ActorEventSource" /> class from being created.
        /// </summary>
        private ActorTrace()
        {
        }

        internal void WriteInfoWithId(string type, string id, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                // Write Informational Trace.
                // Instance.InfoText(id, type, format);
            }
            else
            {
                // Write Informational Trace.
                // Instance.InfoText(id, type, string.Format(CultureInfo.InvariantCulture, format, args));
            }
        }
    }
}
