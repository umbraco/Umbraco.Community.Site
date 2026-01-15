using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length == 0 || args[0] is "-h" or "--help" or "-?")
{
    PrintHelp();
    return 0;
}

var command = args[0];
var options = ParseOptions(args.Skip(1).ToArray());

return command switch
{
    "update-packages" => await UpdatePackagesAsync(options),
    "export-schema" => await ExportSchemaAsync(options),
    "all" => await RunAllAsync(options),
    _ => PrintHelp()
};

int PrintHelp()
{
    Console.WriteLine("""
        Umbraco upgrade helper - updates packages and regenerates Deploy schema

        Usage: upgrade-umbraco <command> [options]

        Commands:
          update-packages  Update all packages in Directory.Packages.props to latest versions
          export-schema    Start the site and regenerate Umbraco Deploy schema files
          all              Run both update-packages and export-schema

        Options for update-packages:
          --dry-run           Show what would be updated without making changes

        Options for export-schema:
          --url <url>         The URL to check for site readiness [default: https://localhost:44383]
          --timeout <seconds> Timeout in seconds to wait for the site to be ready [default: 120]

        Examples:
          upgrade-umbraco update-packages --dry-run
          upgrade-umbraco update-packages
          upgrade-umbraco export-schema
          upgrade-umbraco all
        """);
    return 0;
}

Dictionary<string, string?> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string?>();
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg.StartsWith("--"))
        {
            var key = arg[2..];
            string? value = null;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                value = args[++i];
            }
            options[key] = value;
        }
    }
    return options;
}

async Task<int> UpdatePackagesAsync(Dictionary<string, string?> options)
{
    var dryRun = options.ContainsKey("dry-run");

    var propsPath = FindDirectoryPackagesProps();
    if (propsPath == null)
    {
        Console.Error.WriteLine("Could not find Directory.Packages.props");
        return 1;
    }

    Console.WriteLine($"Found: {propsPath}");
    Console.WriteLine();

    var doc = XDocument.Load(propsPath);
    var packages = doc.Descendants("PackageVersion")
        .Select(e => new
        {
            Element = e,
            Name = e.Attribute("Include")?.Value ?? "",
            Version = e.Attribute("Version")?.Value ?? ""
        })
        .Where(p => !string.IsNullOrEmpty(p.Name))
        .ToList();

    using var http = new HttpClient();
    http.DefaultRequestHeaders.Add("User-Agent", "upgrade-umbraco-cli");

    var updates = new List<(string Name, string OldVersion, string NewVersion)>();

    foreach (var package in packages)
    {
        try
        {
            var latestVersion = await GetLatestVersionAsync(http, package.Name);
            if (latestVersion != null && latestVersion != package.Version)
            {
                updates.Add((package.Name, package.Version, latestVersion));
                Console.WriteLine($"  {package.Name}: {package.Version} -> {latestVersion}");

                if (!dryRun)
                {
                    package.Element.SetAttributeValue("Version", latestVersion);
                }
            }
            else
            {
                Console.WriteLine($"  {package.Name}: {package.Version} (up to date)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {package.Name}: Error - {ex.Message}");
        }
    }

    Console.WriteLine();

    if (updates.Count == 0)
    {
        Console.WriteLine("All packages are up to date!");
        return 0;
    }

    if (dryRun)
    {
        Console.WriteLine($"Dry run: {updates.Count} package(s) would be updated");
    }
    else
    {
        doc.Save(propsPath);
        Console.WriteLine($"Updated {updates.Count} package(s) in Directory.Packages.props");
    }

    return 0;
}

async Task<string?> GetLatestVersionAsync(HttpClient http, string packageName)
{
    var url = $"https://api.nuget.org/v3-flatcontainer/{packageName.ToLowerInvariant()}/index.json";

    try
    {
        var response = await http.GetFromJsonAsync<NuGetVersionsResponse>(url);
        if (response?.Versions == null || response.Versions.Length == 0)
            return null;

        // Filter versions and find the latest stable
        var latest = response.Versions
            .Select(TryParseVersion)
            .Where(v => v != null)
            .Select(v => v!.Value)
            .Where(v => !v.IsPrerelease)
            .OrderByDescending(v => v.Major)
            .ThenByDescending(v => v.Minor)
            .ThenByDescending(v => v.Patch)
            .FirstOrDefault();

        return latest.Original;
    }
    catch
    {
        return null;
    }
}

(int Major, int Minor, int Patch, bool IsPrerelease, string Original)? TryParseVersion(string version)
{
    var match = Regex.Match(version, @"^(\d+)\.(\d+)\.(\d+)(-.*)?$");
    if (!match.Success) return null;

    return (
        int.Parse(match.Groups[1].Value),
        int.Parse(match.Groups[2].Value),
        int.Parse(match.Groups[3].Value),
        match.Groups[4].Success,
        version
    );
}

string? FindDirectoryPackagesProps()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        var path = Path.Combine(dir, "Directory.Packages.props");
        if (File.Exists(path))
            return path;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return null;
}

async Task<int> ExportSchemaAsync(Dictionary<string, string?> options)
{
    var url = options.TryGetValue("url", out var u) && !string.IsNullOrEmpty(u) ? u : "https://localhost:44383";
    var timeout = options.TryGetValue("timeout", out var t) && int.TryParse(t, out var to) ? to : 120;

    var projectDir = FindUmbracoProjectDir();
    if (projectDir == null)
    {
        Console.Error.WriteLine("Could not find UmbracoCommunity.Web.UI project directory");
        return 1;
    }

    Console.WriteLine($"Project directory: {projectDir}");
    Console.WriteLine("Starting the site...");
    Console.WriteLine();

    // Start the dotnet process
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = "run --no-build",
        WorkingDirectory = projectDir,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    using var process = new Process { StartInfo = psi };
    process.OutputDataReceived += (_, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
            Console.WriteLine($"  [site] {e.Data}");
    };
    process.ErrorDataReceived += (_, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
            Console.WriteLine($"  [site] {e.Data}");
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    try
    {
        // Wait for site to be ready
        using var http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });
        http.Timeout = TimeSpan.FromSeconds(10);

        var ready = false;
        var startTime = DateTime.UtcNow;

        Console.WriteLine($"Waiting for site to be ready at {url}...");

        while (!ready && (DateTime.UtcNow - startTime).TotalSeconds < timeout)
        {
            try
            {
                var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    ready = true;
                    Console.WriteLine("Site is ready!");
                }
            }
            catch
            {
                // Site not ready yet
            }

            if (!ready)
            {
                await Task.Delay(2000);
            }
        }

        if (!ready)
        {
            Console.Error.WriteLine($"Timeout waiting for site to be ready after {timeout} seconds");
            return 1;
        }

        // Trigger deploy export
        var deployDir = Path.Combine(projectDir, "umbraco", "Deploy");
        if (!Directory.Exists(deployDir))
        {
            Directory.CreateDirectory(deployDir);
        }

        var exportTrigger = Path.Combine(deployDir, "deploy-export");
        Console.WriteLine();
        Console.WriteLine($"Creating deploy-export trigger file: {exportTrigger}");
        await File.WriteAllTextAsync(exportTrigger, "");

        // Wait for the export to complete (file should be deleted when done)
        Console.WriteLine("Waiting for schema export to complete...");
        var exportStartTime = DateTime.UtcNow;
        var maxExportWait = 60; // seconds

        while (File.Exists(exportTrigger) && (DateTime.UtcNow - exportStartTime).TotalSeconds < maxExportWait)
        {
            await Task.Delay(1000);
        }

        if (File.Exists(exportTrigger))
        {
            Console.WriteLine("Warning: deploy-export file still exists - export may not have completed");
            File.Delete(exportTrigger);
        }
        else
        {
            Console.WriteLine("Schema export completed!");
        }

        // Give it a moment to finish writing files
        await Task.Delay(2000);

        return 0;
    }
    finally
    {
        Console.WriteLine();
        Console.WriteLine("Stopping the site...");

        if (!process.HasExited)
        {
            // Send Ctrl+C / SIGINT
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, we need to kill the process tree
                KillProcessTree(process.Id);
            }
            else
            {
                // On Linux/Mac, send SIGTERM
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
        }

        Console.WriteLine("Site stopped.");
    }
}

void KillProcessTree(int pid)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "taskkill",
            Arguments = $"/T /F /PID {pid}",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi)?.WaitForExit();
    }
    catch
    {
        // Ignore errors
    }
}

string? FindUmbracoProjectDir()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        var srcDir = Path.Combine(dir, "src", "UmbracoCommunity.Web.UI");
        if (Directory.Exists(srcDir))
            return srcDir;

        // Also check if we're already in the project dir
        if (Path.GetFileName(dir) == "UmbracoCommunity.Web.UI" &&
            File.Exists(Path.Combine(dir, "UmbracoCommunity.Web.UI.csproj")))
            return dir;

        dir = Directory.GetParent(dir)?.FullName;
    }
    return null;
}

async Task<int> RunAllAsync(Dictionary<string, string?> options)
{
    var result = await UpdatePackagesAsync(options);
    if (result != 0) return result;

    if (options.ContainsKey("dry-run"))
    {
        Console.WriteLine("Skipping export-schema in dry-run mode");
        return 0;
    }

    Console.WriteLine();
    return await ExportSchemaAsync(options);
}

record NuGetVersionsResponse(string[] Versions);
