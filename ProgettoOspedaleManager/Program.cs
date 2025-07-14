var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHttpClient();
builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.WebHost.UseUrls("http://localhost:5002");

var app = builder.Build();

app.UseCors();

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();