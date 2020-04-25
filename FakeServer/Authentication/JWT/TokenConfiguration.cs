﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FakeServer.Authentication.Jwt
{
    public static class TokenConfiguration
    {
        // secretKey contains a secret passphrase only your server knows
        private static string _secretKey = "mysupersecret_secretkey!123";

        private static SymmetricSecurityKey _signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_secretKey));

        private static TokenValidationParameters _tokenValidationParameters = new TokenValidationParameters
        {
            // The signing key must match!
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,

            // Validate the JWT Issuer (iss) claim
            ValidateIssuer = true,
            ValidIssuer = "ExampleIssuer",

            // Validate the JWT Audience (aud) claim
            ValidateAudience = true,
            ValidAudience = "ExampleAudience",

            // Validate the token expiry
            ValidateLifetime = true,

            // If you want to allow a certain amount of clock drift, set that here:
            ClockSkew = TimeSpan.Zero
        };

        public static void AddJwtBearerAuthentication(this IServiceCollection services)
        {
            services.AddSingleton<TokenBlacklistService>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                                {
                                    options.Audience = _tokenValidationParameters.ValidAudience;
                                    options.ClaimsIssuer = _tokenValidationParameters.ValidIssuer;
                                    options.TokenValidationParameters = _tokenValidationParameters;
                                    options.Events = new JwtBearerEvents()
                                    {
                                        OnTokenValidated = (context) =>
                                        {
                                            var header = context.Request.Headers["Authorization"];

                                            var blacklist = context.HttpContext.RequestServices.GetService<TokenBlacklistService>();
                                            if (blacklist.IsBlacklisted(header.ToString()))
                                            {
                                                context.Response.StatusCode = 401;
                                                context.Fail(new Exception("Authorization token blacklisted"));
                                            }

                                            return Task.CompletedTask;
                                        }
                                    };
                                });

            services.AddSwaggerGen(c =>
            {
                c.AddSwaggerDoc();

                c.DocumentFilter<TokenOperation>();

                c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri(TokenProviderOptions.Path, UriKind.Relative),
                            Scopes = new Dictionary<string, string>()
                        }
                    }
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {{
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtBearerDefaults.AuthenticationScheme
                        }
                    }, new List<string>()
                }});
            });
        }

        public static void UseTokenProviderMiddleware(this IApplicationBuilder app)
        {
            // Add JWT generation endpoint
            var options = new TokenProviderOptions
            {
                Audience = _tokenValidationParameters.ValidAudience,
                Issuer = _tokenValidationParameters.ValidIssuer,
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
            };

            var opts = Options.Create(options);
            app.UseMiddleware<TokenProviderMiddleware>(opts);
            app.UseMiddleware<TokenLogoutMiddleware>();
        }
    }
}