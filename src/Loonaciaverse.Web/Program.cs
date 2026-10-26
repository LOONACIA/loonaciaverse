using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Loonaciaverse.Web;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Program.ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();

public partial class Program
{
    public static void ConfigureServices(IServiceCollection services, string baseAddress)
    {
        services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(baseAddress) });
        services.AddScoped<IContentService, HttpContentService>();
    }
}
