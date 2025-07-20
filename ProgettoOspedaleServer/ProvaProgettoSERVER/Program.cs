using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;
using ProvaProgettoSERVER.Auth;
using ProvaProgettoSERVER.Services;
using ProvaProgettoSERVER;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ospedale API", Version = "v1" });
    c.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Inserisci matricola e password come header Authorization"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { 
                    Type = ReferenceType.SecurityScheme, 
                    Id = "basic" 
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<OspedaleContext>(options =>
{
    var dataSource = "serverospedale.database.windows.net";
    var database = "Ospedale";

    var connection = new SqlConnection
    {
        ConnectionString = $"Server=tcp:{dataSource},1433;Database={database};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    };

    var credential = new DefaultAzureCredential();
    var tokenRequest = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
    var token = credential.GetToken(tokenRequest);
    connection.AccessToken = token.Token;

    options.UseSqlServer(connection);
});

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection(); COMMENTATO PERCHE DA' PROBLEMI DI AUTORIZZAZIONE

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();