using Microsoft.Extensions.Configuration;
using OhMyDemoConsole.Library;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var configurationBuilder = new ConfigurationBuilder()
  .AddSecureJson("appsettings.json", optional: false, reloadOnChange: true);

var overrideFile = ConfigurationHelper.FindOverrideParentConfig("OhMyDemoConfig.json");
if (overrideFile != null)
{
    Log.Information("Found configuration file {overrideConfigFile}", overrideFile);
    configurationBuilder.AddSecureJson(overrideFile, optional:false, reloadOnChange: true);
}

IConfiguration Configuration = configurationBuilder.Build();

var secret = Configuration.GetValue<string>("Secret");
var superSecret = Configuration.GetValue<string>("SuperSecret");

// See https://aka.ms/new-console-template for more information
Console.WriteLine($"My secret is: {secret}");
Console.WriteLine($"My SuperSecret is: {superSecret}");
Console.ReadKey();
