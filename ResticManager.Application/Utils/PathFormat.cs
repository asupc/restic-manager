namespace ResticManager.Application.Utils
{
    public static class PathFormat
    {
        public static string Format(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath))
            {
                return "/";
            }

            dirPath = dirPath.Replace(":\\", "/").Replace("\\", "/");
            if (!dirPath.StartsWith("/"))
            {
                dirPath = $"/{dirPath}";
            }
            return dirPath;
        }
    }
}
