using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Dapr.Client
{
    // temp file to hold state related enums etc
    class StateClasses
    {
    }

    // TODO needs better name?
    public sealed class StateAndETag<TValue>
    {
        public StateAndETag(TValue data, string etag)
        {
            this.Data = data;
            this.ETag = etag;
        }

        public TValue Data { get; }

        public string ETag { get; }
    }
    /// <summary>
    /// 
    /// </summary>
    ///

    public sealed class RetryOptions
    {
        // ZZZ must be converted on use
        public TimeSpan? RetryInterval { get; set; }
        public RetryMode? RetryMode { get; set; }
        public int? RetryThreshold
        {
            get; set;
        }
    }


    public enum ConcurrencyMode
    {
        FirstWrite,
        LastWrite,
    }

    public enum ConsistencyMode
    {
        Strong,
        Eventual,
    }

    public enum RetryMode
    {
        Linear,
        Exponential,
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

