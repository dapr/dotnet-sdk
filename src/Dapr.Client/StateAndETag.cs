// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public sealed class StateAndETag<TValue>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="etag"></param>
        public StateAndETag(TValue data, string etag)
        {
            this.Data = data;
            this.ETag = etag;
        }

        /// <summary>
        /// 
        /// </summary>
        public TValue Data { get; }

        /// <summary>
        /// 
        /// </summary>
        public string ETag { get; }
    }
}
