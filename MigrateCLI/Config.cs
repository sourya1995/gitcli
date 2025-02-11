public class AppConfig
{
    public RepositoryConfig Repository { get; set; }
    public List<FileMapping> FileMappings { get; set; }
    public NetworkConfig Network { get; set; }
    public PullRequestConfig PullRequest { get; set; }
}

public class RepositoryConfig
{
    public string SourceRepository { get; set; }
    public string TargetRepository { get; set; }
    public string CloneDirectory { get; set; }
}

public class FileMapping
{
    public string TargetPath { get; set; }
    public string ContentFile { get; set; }
}

public class NetworkConfig
{
    public List<string> CidrRanges { get; set; }
    public int PingTimeoutMs { get; set; }
}

public class PullRequestConfig
{
    public string Title { get; set; }
    public string BaseBranch { get; set; }
    public string BranchName { get; set; }
    public string Body { get; set; }
    public List<string> Labels { get; set; }
}