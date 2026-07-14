using Microsoft.AspNetCore.Authentication.Cookies;
using PilotsRUs.Admin.App.Infrastructure;
using PilotsRUs.Shared.SDK.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorPages(options => options.Conventions
    .AuthorizeFolder("/Users", "AdminOnly")
    .AuthorizeFolder("/Manufacturers")
    .AuthorizeFolder("/AircraftModels")
    .AuthorizeFolder("/Countries")
    .AuthorizeFolder("/Airports"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole(AuthConstants.AdminRoleName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient("Api", client => client.BaseAddress = new Uri("https+http://api"))
    .AddHttpMessageHandler<BearerTokenHandler>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
