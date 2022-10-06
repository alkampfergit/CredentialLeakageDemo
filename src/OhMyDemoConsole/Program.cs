using Microsoft.Extensions.Configuration;
using OhMyDemoConsole.Library;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var configurationBuilder = new ConfigurationBuilder()
  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var overrideFile = ConfigurationHelper.FindOverrideParentConfig("OhMyDemoConfig.json");
if (overrideFile != null)
{
    Log.Information("Found configuration file {overrideConfigFile}", overrideFile);
    configurationBuilder.AddJsonFile(overrideFile, optional:false, reloadOnChange: true);
}

IConfiguration Configuration = configurationBuilder.Build();

var secret = Configuration.GetValue<string>("Secret");

// See https://aka.ms/new-console-template for more information
Console.WriteLine($"My secret is: {secret}");
Console.ReadKey();
