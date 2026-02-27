using dottop;
using Microsoft.Extensions.Hosting;
using Termina.Hosting;


Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTermina("/", termina =>
{
    termina.RegisterRoute<DashboardPage, DashboardViewModel>("/");
});

await builder.Build().RunAsync();


public record DiskInfo(string Name, ulong Total, ulong Free)
{
    public ulong Used => Total - Free;
    public double UsedPercent => Total > 0 ? (double)Used / Total * 100 : 0;
}

public record ProcessInfo(string PId, string Name, long WorkingSet64);

public record NetworkInfo(string Name, ulong RxPerSec, ulong TxPerSec);