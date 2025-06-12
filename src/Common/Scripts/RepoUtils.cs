namespace Common.Scripts
{
    public static class RepoUtils
    {
        public static string GetRepoName(string url)
        {
            var name = url.Split('/')[^1];
            if (name.EndsWith(".git"))
                name = name[..^4];
            return name;
        }
    }
}

