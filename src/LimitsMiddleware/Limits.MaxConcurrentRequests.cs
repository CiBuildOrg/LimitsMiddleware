﻿namespace LimitsMiddleware
{
    using System;
    using System.Threading;
    using LimitsMiddleware.LibOwin;
    using LimitsMiddleware.Logging;
    using MidFunc = System.Func<
       System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>,
       System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>
       >;

    public static partial class Limits
    {
        /// <summary>
        ///     Limits the number of concurrent requests that can be handled used by the subsequent stages in the owin pipeline.
        /// </summary>
        /// <param name="maxConcurrentRequests">
        ///     The maximum number of concurrent requests. Use 0 or a negative
        ///     number to specify unlimited number of concurrent requests.
        /// </param>
        /// <param name="loggerName">(Optional) The name of the logger log messages are written to.</param>
        /// <returns>An OWIN middleware delegate.</returns>
        public static MidFunc MaxConcurrentRequests(int maxConcurrentRequests, string loggerName = null)
        {
            return MaxConcurrentRequests(() => maxConcurrentRequests);
        }

        /// <summary>
        ///     Limits the number of concurrent requests that can be handled used by the subsequent stages in the owin pipeline.
        /// </summary>
        /// <param name="getMaxConcurrentRequests">
        ///     A delegate to retrieve the maximum number of concurrent requests. Allows you
        ///     to supply different values at runtime. Use 0 or a negative number to specify unlimited number of concurrent
        ///     requests.
        /// </param>
        /// <param name="loggerName">(Optional) The name of the logger log messages are written to.</param>
        /// <returns>An OWIN middleware delegate.</returns>
        /// <exception cref="System.ArgumentNullException">getMaxConcurrentRequests</exception>
        public static MidFunc MaxConcurrentRequests(
            Func<int> getMaxConcurrentRequests,
            string loggerName = null)
        {
            getMaxConcurrentRequests.MustNotNull("getMaxConcurrentRequests");

            return MaxConcurrentRequests(_ => getMaxConcurrentRequests(), loggerName);
        }

        /// <summary>
        ///     Limits the number of concurrent requests that can be handled used by the subsequent stages in the owin pipeline.
        /// </summary>
        /// <param name="getMaxConcurrentRequests">
        ///     A delegate to retrieve the maximum number of concurrent requests. Allows you
        ///     to supply different values at runtime. Use 0 or a negative number to specify unlimited number of concurrent
        ///     requests.
        /// </param>
        /// <param name="loggerName">(Optional) The name of the logger log messages are written to.</param>
        /// <returns>An OWIN middleware delegate.</returns>
        /// <exception cref="System.ArgumentNullException">getMaxConcurrentRequests</exception>
        public static MidFunc MaxConcurrentRequests(
            Func<RequestContext, int> getMaxConcurrentRequests,
            string loggerName = null)
        {
            getMaxConcurrentRequests.MustNotNull("getMaxConcurrentRequests");

            loggerName = string.IsNullOrWhiteSpace(loggerName)
                ? "LimitsMiddleware.MaxConcurrentRequests"
                : loggerName;
            var logger = LogProvider.GetLogger(loggerName);
            var concurrentRequestCounter = 0;

            return
                next =>
                async env =>
                {
                    var owinRequest = new OwinRequest(env);
                    var limitsRequestContext = new RequestContext(owinRequest);
                    int maxConcurrentRequests = getMaxConcurrentRequests(limitsRequestContext);
                    if (maxConcurrentRequests <= 0)
                    {
                        maxConcurrentRequests = int.MaxValue;
                    }
                    try
                    {
                        int concurrentRequests = Interlocked.Increment(ref concurrentRequestCounter);
                        logger.Debug("Concurrent request {0}/{1}.".FormatWith(concurrentRequests, maxConcurrentRequests));
                        if (concurrentRequests > maxConcurrentRequests)
                        {
                            logger.Info("Limit ({0}). Request rejected."
                                .FormatWith(maxConcurrentRequests, concurrentRequests));
                            var response = new OwinContext(env).Response;
                            response.StatusCode = 503;
                            response.ReasonPhrase = "Service Unavailable";
                            response.Write(response.ReasonPhrase);
                            return;
                        }
                        await next(env);
                    }
                    finally
                    {
                        int concurrentRequests = Interlocked.Decrement(ref concurrentRequestCounter);
                        logger.Debug("Concurrent request {0}/{1}.".FormatWith(concurrentRequests, maxConcurrentRequests));
                    }
                };
        }
    }
}
