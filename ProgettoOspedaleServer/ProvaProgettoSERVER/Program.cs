/*using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProvaProgettoSERVER.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<OspedaleContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();*/


using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProvaProgettoSERVER.Auth;
using ProvaProgettoSERVER.Services;
using ProvaProgettoSERVER; // per UserService e ClaimsTransformationService

var builder = WebApplication.CreateBuilder(args);

// 🔌 Aggiungi servizi
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

// ✅ Configura Swagger con autenticazione Basic
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Inserisci matricola e password come header Authorization (es. 'Basic <base64(matricola:password)>')"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "basic" }
            },
            new string[] {}
        }
    });
});

// ✅ Aggiungi il tuo DbContext
builder.Services.AddDbContext<OspedaleContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 🔐 Configura autenticazione Basic
builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

// ✅ Aggiungi supporto per aggiunta dinamica di claim di ruolo
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformationService>();

var app = builder.Build();

// 🌍 Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 🔐 Ordine corretto: prima Auth, poi Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
