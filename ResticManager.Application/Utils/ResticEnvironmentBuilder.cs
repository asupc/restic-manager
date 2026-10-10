using System.Diagnostics;
using System.Text.Json;
using ResticManager.Models;

namespace ResticManager.Application.Utils;

/// <summary>
/// 按仓库类型把 CredentialsJson 凭据映射为 restic 进程环境变量。
/// local/sftp/rclone 无需环境变量；仓库加密密码不在此处（仍走 stdin）。
/// </summary>
public static class ResticEnvironmentBuilder
{
    /// <summary>
    /// 类型 → (CredentialsJson 键 → 环境变量名)
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> Mappings = new(StringComparer.OrdinalIgnoreCase)
    {
        [RepositoryTypes.S3] = new() { ["accessKey"] = "AWS_ACCESS_KEY_ID", ["secretKey"] = "AWS_SECRET_ACCESS_KEY" },
        [RepositoryTypes.B2] = new() { ["accountId"] = "B2_ACCOUNT_ID", ["accountKey"] = "B2_ACCOUNT_KEY" },
        [RepositoryTypes.Azure] = new() { ["accountName"] = "AZURE_ACCOUNT_NAME", ["accountKey"] = "AZURE_ACCOUNT_KEY" },
        [RepositoryTypes.Gs] = new() { ["projectId"] = "GOOGLE_PROJECT_ID", ["credentialsPath"] = "GOOGLE_APPLICATION_CREDENTIALS" },
        [RepositoryTypes.Rest] = new() { ["username"] = "RESTIC_REST_USERNAME", ["password"] = "RESTIC_REST_PASSWORD" },
    };

    /// <summary>
    /// 把仓库凭据写入进程启动信息的环境变量。凭据为空或类型无需环境变量时直接返回；
    /// 非法 JSON 让其抛出（兜底绕过校验直接改库的脏数据）。
    /// </summary>
    public static void Apply(ProcessStartInfo startInfo, Repository repository)
    {
        if (repository == null || string.IsNullOrWhiteSpace(repository.CredentialsJson))
            return;

        if (!Mappings.TryGetValue(repository.Type ?? string.Empty, out var mapping))
            return;

        using var doc = JsonDocument.Parse(repository.CredentialsJson);
        foreach (var (key, envName) in mapping)
        {
            var value = doc.RootElement.TryGetProperty(key, out var element) ? element.GetString() : null;
            if (!string.IsNullOrEmpty(value))
                startInfo.EnvironmentVariables[envName] = value;
        }
    }
}
