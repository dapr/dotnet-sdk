// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;
    using System.Globalization;

    internal sealed class ActorTrace
    {
        internal static readonly ActorTrace Instance = new ActorTrace();
        private readonly ITraceWriter traceWriter;

        /// <summary>
        /// Prevents a default instance of the <see cref="ActorTrace" /> class from being created.
        /// </summary>
        private ActorTrace()
        {
            // TODO: Replace with actual TraceWriter (or integrate with distributed tracing).
            // Use ConsoleTraceWriter during development & test.
            this.traceWriter = new ConsoleTraceWriter();
        }

        /// <summary>
        /// Interface for traces.
        /// </summary>
        private interface ITraceWriter
        {
            /// <summary>
            /// Writes info trace.
            /// </summary>
            /// <param name="infoText">Text to trace.</param>
            void WriteInfo(string infoText);

            /// <summary>
            /// Writes warning trace.
            /// </summary>
            /// <param name="warningText">Text to trace.</param>
            void WriteWarning(string warningText);

            /// <summary>
            /// Writes Error trace.
            /// </summary>
            /// <param name="errorText">Text to trace.</param>
            void WriteError(string errorText);
        }        

        internal void WriteInfo(string type, string format, params object[] args)
        {
            this.WriteInfoWithId(type, string.Empty, format, args);
        }

        internal void WriteInfoWithId(string type, string id, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                this.traceWriter.WriteInfo($"{type}: {id} {format}");
            }
            else
            {
                this.traceWriter.WriteInfo($"{type}: {id} {string.Format(CultureInfo.InvariantCulture, format, args)}");
            }
        }

        internal void WriteWarning(string type, string format, params object[] args)
        {
            this.WriteWarningWithId(type, string.Empty, format, args);
        }

        internal void WriteWarningWithId(string type, string id, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                this.traceWriter.WriteWarning($"{type}: {id} {format}");
            }
            else
            {
                this.traceWriter.WriteWarning($"{type}: {id} {string.Format(CultureInfo.InvariantCulture, format, args)}");
            }
        }

        internal void WriteError(string type, string format, params object[] args)
        {
            this.WriteErrorWithId(type, string.Empty, format, args);
        }

        internal void WriteErrorWithId(string type, string id, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                this.traceWriter.WriteError($"{type}: {id} {format}");
            }
            else
            {
                this.traceWriter.WriteError($"{type}: {id} {string.Format(CultureInfo.InvariantCulture, format, args)}");
            }
        }

        private class ConsoleTraceWriter : ITraceWriter
        {
            public void WriteError(string errorText)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {errorText}");
                Console.ResetColor();
            }

            public void WriteInfo(string infoText)
            {
                Console.WriteLine(infoText);
            }

            public void WriteWarning(string warningText)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"WARNING: {warningText}");
                Console.ResetColor();
            }
        }
    }
}
