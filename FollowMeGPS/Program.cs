using FollowMeGPS.Hubs;
using FollowMeGPS.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<LocationService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<LocationHub>("/locationHub");

app.Run();
