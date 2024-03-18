namespace Dapr.Client
{
    using System;
    using Grpc.Core;

    /// <summary>
    /// gRPC interceptor which adds the required deadline for Dapr gRPC calls.
    /// </summary>
    public class TimeoutCallInvoker : CallInvoker
    {
        private readonly CallInvoker _invoker;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutCallInvoker"/> class.
        /// </summary>
        /// <param name="invoker">The underlying call invoker to wrap.</param>
        /// <param name="timeout">The timeout to apply to all calls.</param>
        public TimeoutCallInvoker(CallInvoker invoker, TimeSpan timeout)
        {
            _invoker = invoker;
            _timeout = timeout;
        }

        /// <summary>
        /// Initiates a blocking unary call with a timeout.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <param name="host">The host where the method is located.</param>
        /// <param name="options">The call options.</param>
        /// <param name="request">The request message.</param>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <returns>The response from the call.</returns>
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
            string host,
            CallOptions options,
            TRequest request)
        {
            var callOptions = options;
            if (options.Deadline == null)
            {
                callOptions = options.WithDeadline(DateTime.UtcNow.Add(_timeout));
            }
            return _invoker.BlockingUnaryCall(method, host, callOptions, request);
        }

        /// <summary>
        /// Initiates an async unary call with a timeout.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <param name="host">The host where the method is located.</param>
        /// <param name="options">The call options.</param>
        /// <param name="request">The request message.</param>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <returns>The response from the call.</returns>
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string host,
            CallOptions options,
            TRequest request)
        {
            var callOptions = options;
            if (options.Deadline == null)
            {
                callOptions = options.WithDeadline(DateTime.UtcNow.Add(_timeout));
            }
            return _invoker.AsyncUnaryCall(method, host, callOptions, request);
        }

        /// <summary>
        /// Initiates an async client streaming call with a timeout.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <param name="host">The host where the method is located.</param>
        /// <param name="options">The call options.</param>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <returns>The response from the call.</returns>
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string host,
            CallOptions options)
        {
            var callOptions = options;
            if (options.Deadline == null)
            {
                callOptions = options.WithDeadline(DateTime.UtcNow.Add(_timeout));
            }
            return _invoker.AsyncClientStreamingCall(method, host, callOptions);
        }

        /// <summary>
        /// Initiates an async server streaming call with a timeout.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <param name="host">The host where the method is located.</param>
        /// <param name="options">The call options.</param>
        /// <param name="request">The request message.</param>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <returns>The response from the call.</returns>
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string host,
            CallOptions options,
            TRequest request)
        {
            var callOptions = options;
            if (options.Deadline == null)
            {
                callOptions = options.WithDeadline(DateTime.UtcNow.Add(_timeout));
            }
            return _invoker.AsyncServerStreamingCall(method, host, callOptions, request);
        }

        /// <summary>
        /// Initiates an async duplex streaming call with a timeout.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <param name="host">The host where the method is located.</param>
        /// <param name="options">The call options.</param>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <returns>The response from the call.</returns>
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string host,
            CallOptions options)
        {
            var callOptions = options;
            if (options.Deadline == null)
            {
                callOptions = options.WithDeadline(DateTime.UtcNow.Add(_timeout));
            }
            return _invoker.AsyncDuplexStreamingCall(method, host, callOptions);
        }

        
    }

}