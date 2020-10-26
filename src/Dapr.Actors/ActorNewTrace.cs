// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    using System;
    using System.Globalization;
    using Microsoft.Extensions.Logging;


    internal sealed class ActorNewTrace
    {
        private readonly ILogger logger;
        private readonly ITraceWriter traceWriter;
        private static ActorNewTrace Instance;

        /// <summary>
        /// Prevents a default instance of the <see cref="ActorTrace" /> class from being created.
        /// </summary>
        internal ActorNewTrace(ILogger logger = null)
        {
            // TODO: Replace with actual TraceWriter (or integrate with distributed tracing).
            // Use ConsoleTraceWriter during development & test.
            var loggerFactory = new LoggerFactory();
            this.logger = logger ?? loggerFactory.CreateLogger("ActorTrace");
            this.traceWriter = new MyTraceWriter(this.logger);
            Instance = this;
            
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

        internal static void WriteInfo(string type, string format, params object[] args)
        {
            WriteInfoWithId(type, string.Empty, format, args);
        }

        internal static void WriteInfoWithId(string type, string id, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                Instance.traceWriter.WriteInfo($"{type}: {id} {format}");
            }
            else
            {
                Instance.logger.LogInformation($"{type}: {id} {string.Format(CultureInfo.InvariantCulture, format, args)}");
                Instance.traceWriter.WriteInfo($"{type}: {id} {string.Format(CultureInfo.InvariantCulture, format, args)}");
            }
        }

        internal static void WriteWarning(string type, string format, params object[] args)
        {
            Instance.logger.LogWarning(type, string.Empty, format, args);
        }

        internal static void WriteWarningWithId(string type, string id, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                Instance.logger.LogWarning($"{type}: {id} {format}");
            }
            else
            {
                Instance.logger.LogWarning($"{type}: {id} {string.Format(CultureInfo.InvariantCulture, format, args)}");
            }
        }

        internal static void WriteError(string type, string format, params object[] args)
        {
            WriteErrorWithId(type, string.Empty, format, args);
        }

        internal static void WriteErrorWithId(string type, string id, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                Instance.logger.LogError($"{type}: {id} {format}");
                Instance.traceWriter.WriteInfo($"{type}: {id} {format}");
            }
            else
            {
                Instance.logger.LogError($"{type}: {id} {string.Format(CultureInfo.InvariantCulture, format, args)}");
                Instance.traceWriter.WriteInfo($"{type}: {id} {format}");
            }
        }

        private class MyTraceWriter : ITraceWriter
        {
            private readonly ILogger logger;
            public MyTraceWriter(ILogger logger)
            {
                this.logger = logger;
            }
            public void WriteError(string errorText)
            {
                this.logger.LogError($"ERROR: {errorText}");
            }

            public void WriteInfo(string infoText)
            {
                this.logger.LogInformation("#######" + infoText);
            }

            public void WriteWarning(string warningText)
            {
                this.logger.LogWarning($"WARNING: {warningText}");
            }
        }
    }
}
