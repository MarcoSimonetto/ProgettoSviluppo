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
    c.AddSecurityDefinition("basic", new OpenApiSecurityScheme      // Aggiunge il supporto per l’autenticazione Basic in Swagger (via intestazione HTTP Authorization).
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Inserisci matricola e password come header Authorization"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement         // Richiede che l’autenticazione Basic venga utilizzata per accedere agli endpoint.
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

builder.Services.AddDbContext<OspedaleContext>(options =>           // Configurazione del database (Entity Framework + Azure SQL)
{
    //  Definisce i parametri della connessione a un database SQL su Azure.
    var dataSource = "serverospedale.database.windows.net";
    var database = "Ospedale";

    var connection = new SqlConnection      //  Crea la connessione SQL Server
    {
        ConnectionString = $"Server=tcp:{dataSource},1433;Database={database};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    };

    //Usa le Azure Managed Identity per autenticarsi con il database in modo sicuro (senza username/password nel codice).
    var credential = new DefaultAzureCredential();
    var tokenRequest = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
    var token = credential.GetToken(tokenRequest);
    connection.AccessToken = token.Token;

    options.UseSqlServer(connection);
});

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null); // Registra il custom handler BasicAuthenticationHandler per gestire login/matricola/password tramite header HTTP.

builder.Services.AddScoped<UserService>();  // gestisce utenti
builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformationService>();   // personalizza claims

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//il metodo dà problemi di UNAUTHORIZED
//app.UseHttpsRedirection();

app.UseAuthentication();   // Autentica l’utente (deve venire prima dell’autorizzazione)
app.UseAuthorization();    // Controlla se l’utente ha i permessi per accedere

app.MapControllers();       // Mappa gli endpoint definiti nei controller

app.Run();