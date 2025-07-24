var builder = WebApplication.CreateBuilder(args);


//CORS Cross-Origin Resource Sharing
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin() //Permette richieste da qualsiasi origine
              .AllowAnyHeader() //Permette qualsiasi header HTTP (come Authorization)
              .AllowAnyMethod(); //Permette GET, POST, PUT, DELETE,
    });
});

//registra il servizio, l'autorizazzione ASP.NET core e i controller che rispondono alle chiamate HTTP
builder.Services.AddHttpClient();
builder.Services.AddAuthorization();
builder.Services.AddControllers();

//si mette in ascolto su 5002
builder.WebHost.UseUrls("http://localhost:5002");

//compila 
var app = builder.Build();

app.UseCors();

app.UseRouting();
app.UseAuthorization();
//Mappa i controller definiti con [ApiController] e [Route] per rispondere alle richieste.
app.MapControllers();

app.Run();