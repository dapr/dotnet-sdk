// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore
{
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

        public DaprAuthenticationHandler(
            IOptionsMonitor<DaprAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder, 
            ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

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
                return AuthenticateResult.Fail("Dapr API Token not configured.");
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
}
