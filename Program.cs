using WppConnect4Aspnet.Data;
using WppConnect4Aspnet.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.AllowAnyOrigin()
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

builder.Services.AddHttpClient();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options =>
        options.UseSqlServer(connectionString));

builder.Services.AddSingleton<IWaJsService, WaJsService>();
builder.Services.AddSingleton<IPuppeteerWppService, PuppeteerWppService>();



builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "WppConnect4Aspnet API", Version = "v1" });
});

var app = builder.Build();
var wppServie = app.Services.GetRequiredService<IPuppeteerWppService>();
await wppServie.StartSessionsFromDbAsync();

using (var scope = app.Services.CreateScope())
{
    var waJsService = scope.ServiceProvider.GetRequiredService<IWaJsService>();
    await waJsService.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthorization();
app.MapControllers();
app.Run();