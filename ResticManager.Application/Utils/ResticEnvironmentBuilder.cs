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
    /// 启动 restic 前必须清除的宿主机环境变量，避免与当前仓库配置冲突或覆盖 stdin 密码。
    /// </summary>
    private static readonly string[] ConflictingEnvVars =
    [
        "RESTIC_PASSWORD",
        "RESTIC_PASSWORD_FILE",
        "RESTIC_PASSWORD_COMMAND",
        "AWS_ACCESS_KEY_ID",
        "AWS_SECRET_ACCESS_KEY",
        "AWS_DEFAULT_REGION",
        "B2_ACCOUNT_ID",
        "B2_ACCOUNT_KEY",
        "AZURE_ACCOUNT_NAME",
        "AZURE_ACCOUNT_KEY",
        "GOOGLE_PROJECT_ID",
        "GOOGLE_APPLICATION_CREDENTIALS",
        "RESTIC_REST_USERNAME",
        "RESTIC_REST_PASSWORD"
    ];

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
    /// 把仓库凭据写入进程启动信息的环境变量。先清除可能从宿主机继承的冲突变量；
    /// 凭据为空或类型无需环境变量时直接返回；非法 JSON 或结构不合法时抛出异常。
    /// </summary>
    public static void Apply(ProcessStartInfo startInfo, Repository repository)
    {
        foreach (var envVar in ConflictingEnvVars)
        {
            startInfo.EnvironmentVariables.Remove(envVar);
        }

        if (repository == null || string.IsNullOrWhiteSpace(repository.CredentialsJson))
            return;

        if (!Mappings.TryGetValue(repository.Type ?? string.Empty, out var mapping))
            return;

        using var doc = JsonDocument.Parse(repository.CredentialsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("仓库凭据 JSON 必须为对象格式");

        foreach (var (key, envName) in mapping)
        {
            if (!doc.RootElement.TryGetProperty(key, out var element) || element.ValueKind == JsonValueKind.Null)
                continue;

            if (element.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"仓库凭据字段 {key} 必须为字符串类型");

            var value = element.GetString();
            if (!string.IsNullOrEmpty(value))
                startInfo.EnvironmentVariables[envName] = value;
        }
    }
}
