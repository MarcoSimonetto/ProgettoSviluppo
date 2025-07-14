using Microsoft.AspNetCore.Mvc;
using ProvaMVC.Models;

namespace ProvaMVC.Controllers;

public class RepartiController : Controller
{
    private readonly HttpClient Client;

    public RepartiController(IHttpClientFactory clientFactory)
    {
        Client = clientFactory.CreateClient();
        Client.BaseAddress = new Uri("http://localhost:5002");
    }

}
