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

// 🔌 Aggiungi servizi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ✅ Swagger con autenticazione Basic
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

// ✅ Connessione ad Azure SQL con Azure AD (DefaultAzureCredential)
builder.Services.AddDbContext<OspedaleContext>(options =>
{
    var dataSource = "serverospedale.database.windows.net"; // 🔁 Sostituisci con il tuo server
    var database = "Ospedale"; // 🔁 Sostituisci con il tuo database

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

// 🔐 Basic Authentication
builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

// ✅ Claims Transformation
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformationService>();

var app = builder.Build();

// 🌍 Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection(); COMMENTATO PERCHE LA REDIRECTION SBALLA LA AUTH

// 🔐 Ordine: Auth → Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();