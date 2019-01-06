﻿// Copyright 2017 the original author or authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Steeltoe.Management.Endpoint.CloudFoundry;
using Steeltoe.Management.Endpoint.Middleware;
using Steeltoe.Management.EndpointBase.Security;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Steeltoe.Management.Endpoint.Security
{
    public class SecurityMiddleware
    {
        private RequestDelegate _next;
        private ILogger<SecurityMiddleware> _logger;
        private ICloudFoundryOptions _options;
        private SecurityHelper _helper;

        public SecurityMiddleware(RequestDelegate next, ICloudFoundryOptions options, ILogger<SecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _options = options;
            _helper = new SecurityHelper(options, logger);
        }

        public async Task Invoke(HttpContext context)
        {
            _logger.LogDebug("Invoke({0})", context.Request.Path.Value);

            if (_helper.IsCloudFoundryRequest(context.Request.Path) && _options.IsEnabled)
            {
                IEndpointOptions target = FindTargetEndpoint(context.Request.Path);
                if (target == null)
                {
                    await _helper.ReturnError(
                        context,
                        new SecurityResult(HttpStatusCode.ServiceUnavailable, _helper.ENDPOINT_NOT_CONFIGURED_MESSAGE));
                    return;
                }

                if (target.IsSensitive && !HasSensitiveClaim(context))
                {
                    await _helper.ReturnError(
                        context,
                        new SecurityResult(HttpStatusCode.Unauthorized, _helper.ACCESS_DENIED_MESSAGE));

                    return;
                }
            }

            await _next(context);
        }

        private bool HasSensitiveClaim(HttpContext context)
        {
            var claim = _options.Global.SensitiveClaim;
            var user = context.User;
            return user.Identity.IsAuthenticated && user.HasClaim(claim.Type, claim.Value);
        }

        private IEndpointOptions FindTargetEndpoint(PathString path)
        {
            var configEndpoints = this._options.Global.EndpointOptions;
            foreach (var ep in configEndpoints)
            {
                PathString epPath = new PathString(ep.Path);
               if (path.Value == ep.Path)
                {
                    return ep;
                }
            }

            return null;
        }
    }
}