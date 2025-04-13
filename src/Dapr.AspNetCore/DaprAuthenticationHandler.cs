// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.AspNetCore;

using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal class DaprAuthenticationHandler : AuthenticationHandler<DaprAuthenticationOptions>
{
    const string DaprApiToken = "Dapr-Api-Token";

#if NET8_0_OR_GREATER
    public DaprAuthenticationHandler(
        IOptionsMonitor<DaprAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }
#else
        public DaprAuthenticationHandler(
            IOptionsMonitor<DaprAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }
#endif

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        return Task.FromResult(HandleAuthenticate());
    }

    private AuthenticateResult HandleAuthenticate()
    {
        if (!Request.Headers.TryGetValue(DaprApiToken, out var token))
        {
            return AuthenticateResult.NoResult();
        }

        var expectedToken = Options.Token;
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return AuthenticateResult.Fail("App API Token not configured.");
        }

        if (!string.Equals(token, expectedToken))
        {
            return AuthenticateResult.Fail("Not authenticated.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "Dapr")
        };
        var identity = new ClaimsIdentity(claims, Options.Scheme);
        var identities = new List<ClaimsIdentity> { identity };
        var principal = new ClaimsPrincipal(identities);
        var ticket = new AuthenticationTicket(principal, Options.Scheme);

        return AuthenticateResult.Success(ticket);
    }
}