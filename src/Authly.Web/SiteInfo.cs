namespace Authly.Web;

/// <summary>
/// Single source of truth for public-marketing-site links and metadata.
/// Update <see cref="GitHubUrl"/> / <see cref="License"/> once the repo is published.
/// </summary>
public static class SiteInfo
{
    public const string Author = "Abhishek Raheja";
    public const string GitHubUrl = "https://github.com/abhiraheja/authly";
    public const string License = "Open source";
    public const string Tagline = "Free, open-source, self-hostable identity infrastructure.";

    /// <summary>Published Docker Hub image — the no-source self-host path pulls this.</summary>
    public const string DockerImage = "abhiraheja/authly";
}
