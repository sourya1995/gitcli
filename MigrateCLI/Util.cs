using System.Text.Json;

static async Task CreateFilesAndRaisePRAsync()
{
    try
    {
        // Load configuration
        var configPath = Path.Combine(Environment.CurrentDirectory, "config.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("Configuration file not found", configPath);

        var config = JsonSerializer.Deserialize<AppConfig>(
            await File.ReadAllTextAsync(configPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var currentDir = Environment.CurrentDirectory;
        var targetRepoDir = Path.Combine(currentDir, config.Repository.CloneDirectory);
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        // Clean and clone repository
        if (Directory.Exists(targetRepoDir))
            Directory.Delete(targetRepoDir, true);

        await RunProcessAsync("git", $"clone {config.Repository.TargetRepository} {config.Repository.CloneDirectory}", currentDir);

        // Create files
        foreach (var mapping in config.FileMappings)
        {
            var fullPath = Path.Combine(targetRepoDir, mapping.TargetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            
            if (!File.Exists(mapping.ContentFile))
                throw new FileNotFoundException($"Content file missing: {mapping.ContentFile}");

            var content = await File.ReadAllTextAsync(mapping.ContentFile);
            await File.WriteAllTextAsync(fullPath, content);
        }

        // Git operations
        await RunProcessAsync("git", $"checkout -b {config.PullRequest.BranchName}", targetRepoDir);
        await RunProcessAsync("git", "add .", targetRepoDir);
        await RunProcessAsync("git", $"commit -m \"{config.PullRequest.Title}\"", targetRepoDir);
        await RunProcessAsync("git", $"push origin {config.PullRequest.BranchName}", targetRepoDir);

        // Create PR
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ConfigUpdater", "1.0"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);

        var prData = new
        {
            title = config.PullRequest.Title,
            head = config.PullRequest.BranchName,
            @base = config.PullRequest.BaseBranch,
            body = config.PullRequest.Body,
            labels = config.PullRequest.Labels
        };

        var repoPath = new Uri(config.Repository.TargetRepository).AbsolutePath.Trim('/');
        var response = await client.PostAsJsonAsync(
            $"https://api.github.com/repos/{repoPath}/pulls", 
            prData
        );

        Console.WriteLine($"PR created: {response.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

static async Task PingMachinesAsync()
{
    var configPath = Path.Combine(Environment.CurrentDirectory, "config.json");
    if (!File.Exists(configPath)) return;

    var config = JsonSerializer.Deserialize<AppConfig>(
        await File.ReadAllTextAsync(configPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    foreach (var cidr in config.Network.CidrRanges)
    {
        var ips = GetIPAddressesFromCidr(cidr);
        var pingTasks = ips.Select(ip => PingAsync(ip, config.Network.PingTimeoutMs));
        await Task.WhenAll(pingTasks);
    }
}

static async Task PingAsync(IPAddress ip, int timeout)
{
    using var ping = new Ping();
    try
    {
        var reply = await ping.SendPingAsync(ip, timeout);
        Console.WriteLine($"{ip}: {(reply.Status == IPStatus.Success ? "Success" : "Failed")}");
    }
    catch
    {
        Console.WriteLine($"{ip}: Error");
    }
}