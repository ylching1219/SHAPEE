global using SHAPEE.Models;
global using SHAPEE;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

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