using dottop;
using Microsoft.Extensions.Hosting;
using Termina.Hosting;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTermina("/", termina => { termina.RegisterRoute<DashboardPage, DashboardViewModel>("/"); });

await builder.Build().RunAsync();