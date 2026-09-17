using Identity.Api.Data;
using Identity.Api.Data.Entities;
using Identity.Api.Data.Repositories;
using Identity.Api.DomainComponents;
using Identity.Api.Middleware;
using Identity.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<EnTrackBag.Sessions.SessionRepository>();
builder.Services.AddHostedService<EnTrackBag.Sessions.SessionExpiryWorker>();
builder.Host.UseWindowsService();
builder.Services.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("BLTSMFT")));

builder.Services.AddScoped<IPasswordHasher<UserEntity>, Pbkdf2Sha512PasswordHasher>();
builder.Services.AddSingleton<IPassportProtector, PassportProtector>();
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<IIdentityDomainComponent, IdentityDomainComponent>();
builder.Services.AddScoped<IAdministrationRepository, AdministrationRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserDomainComponent, UserDomainComponent>();
builder.Services.AddScoped<IRoleDomainComponent, RoleDomainComponent>();
builder.Services.AddScoped<ISessionDomainComponent, SessionDomainComponent>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("ui", p => p.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:4200")
.AllowAnyHeader()
.AllowAnyMethod()
.AllowCredentials()));

var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]),
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]),
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    });

builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
    options.Events.OnTokenValidated = EnTrackBag.Sessions.SessionRepository.ValidateTokenAsync);

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in EnTrackBag.Authorization.PermissionCodes.All)
    {
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission_access", permission + ":" + EnTrackBag.Authorization.AccessTypeCodes.View));
        foreach (var access in EnTrackBag.Authorization.AccessTypeCodes.All)
            options.AddPolicy(permission + ":" + access, policy => policy.RequireAuthenticatedUser().RequireClaim("permission_access", permission + ":" + access));
    }
    options.AddPolicy("Users.Sensitive", policy => policy.RequireRole("Admin").RequireClaim("permission_access", "Users:VIEW"));
});
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<EnTrackBag.Sessions.SessionActivityMiddleware>();
app.MapControllers();
app.Run();
