﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Owin;

namespace Owin.Limits {
    internal class MaxQueryStringMiddleware {
        private readonly Func<IDictionary<string, object>, Task> _next;
        private readonly Func<int> _getMaxQueryStringLength;

        public MaxQueryStringMiddleware(Func<IDictionary<string, object>, Task> next, Func<int> getMaxQueryStringLength) {
            if (next == null) {
                throw new ArgumentNullException("next");
            }
            if (getMaxQueryStringLength == null) {
                throw new ArgumentNullException("getMaxQueryStringLength");
            }

            _next = next;
            _getMaxQueryStringLength = getMaxQueryStringLength;
        }

        public async Task Invoke(IDictionary<string, object> environment) {
            if (environment == null) {
                throw new ArgumentNullException("environment");
            }
            
            int maxQueryStringLength = _getMaxQueryStringLength();
            var context = new OwinContext(environment);
            var queryString = context.Request.QueryString;
            if (queryString.HasValue) {
                var unescapedQueryString = Uri.UnescapeDataString(queryString.Value);
                if (unescapedQueryString.Length > maxQueryStringLength) {
                    context.Response.StatusCode = 414;
                    context.Response.ReasonPhrase = string.Format("The (unescaped) querystring is too long. Only {0} characters are allowed.", maxQueryStringLength);
                    return;
                }
            }

            await _next(environment);
        }
    }
}