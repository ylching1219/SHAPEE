global using SHAPEE;
global using SHAPEE.Models;
using Microsoft.AspNetCore.HttpOverrides;
using QuestPDF.Infrastructure;
using SHAPEE.Services;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<LeaveService>();
builder.Services.AddControllersWithViews();
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});


builder.Services.AddSqlServer<DB>($@"
    Data Source=(localdb)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;"
);

builder.Services.AddScoped<Helper>();

builder.Services.AddAuthentication().AddCookie();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.UseForwardedHeaders();   
app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapDefaultControllerRoute();
app.Run();