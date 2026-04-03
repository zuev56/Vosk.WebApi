using System.Reflection;

namespace Vosk.Common;

// TODO: Скопипастил из ZS.Common
public static class Extensions
{
    public static IHostApplicationBuilder ConfigureExternalAppConfiguration(this IHostApplicationBuilder builder, string[] args, Assembly assembly)
    {
        builder.Configuration.TryLoadConfigurationJsonFromArguments(assembly, args);

        var configFiles = builder.Configuration.GetAppliedConfigurationFileNames();
        Console.WriteLine($"Applied configuration files: {string.Join(", ", configFiles)}");

        return builder;
    }

    public static bool TryLoadConfigurationJsonFromArguments(
        this IConfigurationBuilder configuration, Assembly assembly, string[]? args)
    {
        if (args?.Any() != true)
            return false;

        var loaded = false;
        var assemblyName = assembly.GetName().Name!;

        foreach (var arg in args.Distinct().Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            if (File.Exists(arg) && Path.GetExtension(arg).Equals(".json", StringComparison.CurrentCultureIgnoreCase))
            {
                configuration.AddJsonFile(arg);
                loaded = true;
            }

            var configFilePath = Path.Combine(arg, $"{assemblyName}.json");
            if (File.Exists(configFilePath))
            {
                configuration.AddJsonFile(configFilePath);
                loaded = true;
            }
        }

        return loaded;
    }

    public static List<string> GetAppliedConfigurationFileNames(this IConfigurationBuilder configuration)
        => configuration.Sources
            .Where(s => s is FileConfigurationSource)
            .Select(s => ((FileConfigurationSource)s).Path)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList()!;
}