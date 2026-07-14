using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Features.Auth;

namespace PilotsRUs.API.WebApi.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static TBuilder AddApplicationIdentity<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Identity's UserStore/RoleStore require a scoped DbContext, so AddNpgsqlDbContext (Aspire's
        // integration, which also wires connection resiliency + a health check) covers that. Any future
        // non-Identity repository code should resolve IDbContextFactory<ApplicationDbContext> instead, per
        // CLAUDE.md's IDbContextFactory convention - both registrations coexist against the same context
        // type without conflict since they resolve via different service types. Don't collapse this into a
        // single registration style.
        builder.AddNpgsqlDbContext<ApplicationDbContext>("pilotsrus");

        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("pilotsrus")));

        // SignInManager requires IAuthenticationSchemeProvider, which AddAuthentication() registers.
        // The actual scheme (JWT bearer) is added later in Program.cs.
        builder.Services.AddAuthentication();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager();

        builder.Services
            .AddOptions<Argon2Options>()
            .Bind(builder.Configuration.GetSection(Argon2Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // AddIdentityCore registers the default PasswordHasher<TUser> via TryAddScoped; this explicit
        // (non-TryAdd) registration overrides it - same override pattern AddApplicationJwtAuth already
        // uses for AddAuthentication(JwtBearerDefaults.AuthenticationScheme). Singleton, not scoped -
        // Argon2PasswordHasher holds no per-instance state and touches no database, unlike
        // IRefreshTokenService below (same stateless reasoning as IJwtTokenService).
        builder.Services.AddSingleton<IPasswordHasher<ApplicationUser>, Argon2PasswordHasher>();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.User.RequireUniqueEmail = true;
        });

        if (builder.Environment.IsDevelopment())
        {
            // Intentionally permissive, Development only - there is no registration endpoint yet
            // (infra-only scope, see CLAUDE.md), so this only ever affects the seeded dev admin. Revisit
            // once real user registration ships and a real password policy needs enforcing in every
            // environment against user-chosen passwords.
            builder.Services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequiredLength = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
            });
        }

        return builder;
    }

    public static TBuilder AddApplicationJwtAuth<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services
            .AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

        builder.Services
            .AddOptions<RefreshTokenOptions>()
            .Bind(builder.Configuration.GetSection(RefreshTokenOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Scoped, not singleton like IJwtTokenService - this one touches the database per call.
        builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configure JwtBearerOptions by resolving IOptions<JwtOptions> from DI rather than reading
        // builder.Configuration directly here - the latter would capture whatever configuration exists at
        // this point in Program.cs's startup, missing sources added afterwards (e.g. in tests).
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((jwtBearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    // Default is 5 minutes, which would let an "expired" access token keep working for up
                    // to 5 minutes past its stated Jwt:Expiry - defeating the point of pairing a
                    // short-lived access token with a refresh token. Access tokens must expire exactly
                    // when they say they do.
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }
}
