namespace ResticManager.Models;

/// <summary>
/// 支持的 restic 仓库类型常量
/// </summary>
public static class RepositoryTypes
{
    public const string Local = "local";
    public const string Sftp = "sftp";
    public const string Rest = "rest";
    public const string S3 = "s3";
    public const string B2 = "b2";
    public const string Azure = "azure";
    public const string Gs = "gs";
    public const string Rclone = "rclone";

    /// <summary>
    /// 全部受支持的仓库类型白名单
    /// </summary>
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Local, Sftp, Rest, S3, B2, Azure, Gs, Rclone
    };
}
