using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Application.Utils;
using ResticManager.Models;
using ResticManager.Models.Dtos;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ResticManager.Application.Services.Implements;

public class ResticService : IResticService
{
    private readonly string _resticPath;
    private readonly ILogger<ResticService> _logger;

    public ResticService(IConfiguration configuration, ILogger<ResticService> logger)
    {
        _resticPath = configuration["Restic:Path"];
        _resticPath = string.IsNullOrEmpty(_resticPath) ? "restic" : _resticPath;
        _logger = logger;
    }


    public async Task InitializeRepositoryAsync(string repoPath, string password)
    {
        var arguments = $"init --repo \"{repoPath}\"";
        // 使用参数数组方式传递密码（需要两次输入：设置密码和确认密码）
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password, password);

        if (exitCode != 0)
        {
            _logger.LogError("初始化仓库失败: {Error}", error);
            throw new Exception($"初始化仓库失败: {error}");
        }

        _logger.LogInformation("仓库初始化成功: {RepoPath}", repoPath);
    }

    public async Task<BackupResult> CreateBackupAsync(string sourcePath, string repoPath, string password, string includePatterns = null, string excludePatterns = null, BackupOptions options = null)
    {
        var args = $"backup \"{sourcePath}\" --repo \"{repoPath}\" --json";

        if (!string.IsNullOrEmpty(includePatterns))
        {
            var patterns = includePatterns.Split(';');
            foreach (var pattern in patterns)
            {
                args += $" --include \"{pattern.Trim()}\"";
            }
        }

        if (!string.IsNullOrEmpty(excludePatterns))
        {
            var patterns = excludePatterns.Split(';');
            foreach (var pattern in patterns)
            {
                args += $" --exclude \"{pattern.Trim()}\"";
            }
        }

        if (options != null)
        {
            if (options.Tags != null)
            {
                foreach (var tag in options.Tags.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    args += $" --tag \"{tag.Trim()}\"";
                }
            }
            if (options.ExcludeCaches)
            {
                args += " --exclude-caches";
            }
            if (!string.IsNullOrWhiteSpace(options.ExcludeLargerThan))
            {
                args += $" --exclude-larger-than {options.ExcludeLargerThan.Trim()}";
            }
            if (options.OneFileSystem)
            {
                args += " --one-file-system";
            }
            if (options.DryRun)
            {
                args += " --dry-run";
            }
        }

        var (outputLines, errorLines, exitCode) = await ExecuteResticCommandWithLineOutputAsync(args, password);

        if (exitCode != 0)
        {
            var errorOutput = string.Join(Environment.NewLine, errorLines);
            _logger.LogError("备份失败: {Error}", errorOutput);
            return new BackupResult { Success = false, Error = $"备份失败: {errorOutput}" };
        }

        // 解析输出，查找summary消息
        var summaryLine = outputLines.FirstOrDefault(line =>
            line.Contains("\"message_type\":\"summary\""));

        if (summaryLine != null)
        {
            try
            {
                var summary = JsonSerializer.Deserialize<BackupSummary>(summaryLine);
                _logger.LogInformation("备份成功完成: {SourcePath}", sourcePath);
                return new BackupResult
                {
                    Success = true,
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                // 如果解析失败，返回原始输出
                var fullOutput = string.Join(Environment.NewLine, outputLines);
                _logger.LogWarning("备份成功但解析摘要失败: {Error}", ex.Message);
                return new BackupResult { Success = true, RawOutput = fullOutput, Error = ex.Message };
            }
        }
        else
        {
            // 如果没有找到summary，返回所有输出
            var fullOutput = string.Join(Environment.NewLine, outputLines);
            _logger.LogInformation("备份完成但未找到摘要信息");
            return new BackupResult { Success = true, RawOutput = fullOutput };
        }
    }

    public async Task<List<SnapshotInfo>> ListSnapshotsAsync(string repoPath, string password)
    {
        var arguments = $"snapshots --repo \"{repoPath}\" --json";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("获取快照列表失败: {Error}", error);
            throw new Exception($"获取快照列表失败: {error}");
        }

        try
        {
            var snapshots = JsonSerializer.Deserialize<List<SnapshotInfo>>(output);
            _logger.LogInformation("成功获取快照列表");
            return (snapshots ?? []).OrderByDescending(n => n.Time).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError("解析快照列表失败: {Error}", ex.Message);
            throw new Exception($"解析快照列表失败: {ex.Message}");
        }
    }

    public async Task<List<SnapshotInfo>> ListSnapshotsByIdsAsync(string repoPath, string password, IEnumerable<string> ids)
    {
        if (ids == null)
            throw new ArgumentNullException(nameof(ids));

        // 过滤掉空 ID
        var idList = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (idList.Count == 0)
            return new List<SnapshotInfo>();

        // 快照 ID 只能是 4~64 位十六进制（完整 ID 64 位，前缀最短 4 位），
        // 拒绝其余字符，防止通过路由传入的 snapshotId 拼进命令行注入 restic 参数
        foreach (var id in idList)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-fA-F0-9]{4,64}$"))
                throw new ArgumentException($"非法的快照 ID: {id}", nameof(ids));
        }

        // restic 0.18.1: `snapshots <id1> <id2> ... --json` 支持按 ID 过滤
        var idArgs = string.Join(" ", idList.Select(id => $"\"{id}\""));
        var arguments = $"snapshots --repo \"{repoPath}\" --json {idArgs}";

        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("按ID获取快照失败: {Error}", error);
            throw new Exception($"按ID获取快照失败: {error}");
        }

        try
        {
            var snapshots = JsonSerializer.Deserialize<List<SnapshotInfo>>(output);
            _logger.LogInformation("按ID获取快照成功，共 {Count} 条", snapshots?.Count ?? 0);
            return snapshots ?? new List<SnapshotInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError("解析按ID快照列表失败: {Error}", ex.Message);
            throw new Exception($"解析按ID快照列表失败: {ex.Message}");
        }
    }


    public async Task<List<ResticFileInfo>> ListFilesInSnapshotAsync(string repoPath, string password, string snapshotId, string dirPath)
    {
        var formattedDirPath = dirPath.Replace(":\\", "/").Replace("\\", "/");
        if (string.IsNullOrEmpty(formattedDirPath))
        {
            formattedDirPath = "/";
        }
        if (!formattedDirPath.StartsWith("/"))
        {
            formattedDirPath = $"/{formattedDirPath}";
        }

        // 移除末尾的斜杠（根目录除外）
        if (formattedDirPath.Length > 1 && formattedDirPath.EndsWith("/"))
        {
            formattedDirPath = formattedDirPath.TrimEnd('/');
        }

        var arguments = $@"-r ""{repoPath}"" ls {snapshotId} ""{formattedDirPath}"" --json";
        Console.WriteLine($"Arguments：{arguments}");

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        var (outputLines, errorLines, exitCode) = await ExecuteResticCommandWithLineOutputAsync(arguments, password);

        if (exitCode != 0)
        {
            var errorOutput = string.Join(Environment.NewLine, errorLines);
            _logger.LogError("获取文件列表失败: {Error}", errorOutput);
            throw new Exception($"获取文件列表失败: {errorOutput}");
        }

        List<ResticFileInfo> files = [];
        foreach (var line in outputLines)
        {
            try
            {
                var file = JsonSerializer.Deserialize<ResticFileInfo>(line);
                if (file != null)
                {
                    // 如果路径为空但有路径列表，则使用第一个路径
                    if (string.IsNullOrEmpty(file.Path) && file.Paths?.Count > 0)
                    {
                        file.Path = file.Paths.First();
                    }

                    // 确保名称不为空
                    if (string.IsNullOrEmpty(file.Name) && !string.IsNullOrEmpty(file.Path))
                    {
                        file.Name = Path.GetFileName(file.Path.TrimEnd('/'));
                    }

                    files.Add(file);
                }
            }
            catch
            {
                // 忽略解析失败的行
            }
        }

        stopwatch.Stop();
        _logger.LogInformation($"成功获取文件列表，共 {files.Count} 个文件,耗时：{stopwatch.ElapsedMilliseconds} ms");

        // 如果返回的是目录本身的信息，我们需要获取该目录下的文件
        if (files.Count(f => !string.IsNullOrEmpty(f.Type)) == 0 && files.Count == 1)
        {
            // 重新执行命令获取目录内容
            var targetPath = files.FirstOrDefault()?.Paths?.FirstOrDefault() ?? formattedDirPath;
            var newArgs = $@"-r ""{repoPath}"" ls {snapshotId} ""{targetPath}"" --json";
            var (retryOutputLines, retryErrorLines, retryExitCode) = await ExecuteResticCommandWithLineOutputAsync(newArgs, password);

            if (retryExitCode != 0)
            {
                var errorOutput = string.Join(Environment.NewLine, retryErrorLines);
                _logger.LogError("获取文件列表失败: {Error}", errorOutput);
                throw new Exception($"获取文件列表失败: {errorOutput}");
            }

            files.Clear();
            foreach (var line in retryOutputLines)
            {
                try
                {
                    var file = JsonSerializer.Deserialize<ResticFileInfo>(line);
                    if (file != null && !string.IsNullOrEmpty(file.Type))
                    {
                        // 如果路径为空但有路径列表，则使用第一个路径
                        if (string.IsNullOrEmpty(file.Path) && file.Paths?.Count > 0)
                        {
                            file.Path = file.Paths.First();
                        }

                        // 确保名称不为空
                        if (string.IsNullOrEmpty(file.Name) && !string.IsNullOrEmpty(file.Path))
                        {
                            file.Name = Path.GetFileName(file.Path.TrimEnd('/'));
                        }

                        files.Add(file);
                    }
                }
                catch
                {
                    // 忽略解析失败的行
                }
            }
        }

        return files.Where(n => !string.IsNullOrEmpty(n.Type)).ToList();
    }


    public async Task<SnapshotDiffResult> DiffSnapshotsAsync(string repoPath, string password, string snapshotId1, string snapshotId2)
    {
        // 先尝试使用--json参数获取JSON格式输出
        var arguments = $"diff {snapshotId1} {snapshotId2} --repo \"{repoPath}\" --json";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        // 如果不支持--json参数，则使用默认输出
        if (exitCode != 0 && error.Contains("--json"))
        {
            arguments = $"diff {snapshotId1} {snapshotId2} --repo \"{repoPath}\"";
            (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);
        }

        if (exitCode != 0)
        {
            _logger.LogError("比较快照差异失败: {Error}", error);
            throw new Exception($"比较快照差异失败: {error}");
        }

        var diffResult = new SnapshotDiffResult();
        
        // 解析JSON Lines格式的输出
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                // 尝试反序列化每一行
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                
                if (root.TryGetProperty("message_type", out var messageTypeElement))
                {
                    var messageType = messageTypeElement.GetString();
                    if (messageType == "change")
                    {
                        var change = JsonSerializer.Deserialize<SnapshotDiffChange>(line);
                        diffResult.Changes.Add(change);
                    }
                    else if (messageType == "statistics")
                    {
                        diffResult.Statistics = JsonSerializer.Deserialize<SnapshotDiffStatistics>(line);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("解析差异输出行失败: {Line}, 错误: {Error}", line, ex.Message);
            }
        }

        _logger.LogInformation("快照差异比较成功: {SnapshotId1} vs {SnapshotId2}", snapshotId1, snapshotId2);
        return diffResult;
    }

    public async Task RestoreSnapshotAsync(string repoPath, string password, string snapshotId, string targetPath, bool verify = false)
    {
        var arguments = $"restore {snapshotId} --repo \"{repoPath}\" --target \"{targetPath}\"";
        if (verify)
        {
            arguments += " --verify";
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("恢复快照失败: {Error}", error);
            throw new Exception($"恢复快照失败: {error}");
        }

        _logger.LogInformation("快照恢复成功: {SnapshotId} 到 {TargetPath}", snapshotId, targetPath);
    }

    public async Task RestoreFileAsync(string repoPath, string password, string snapshotId, string filePath, string restorePath, bool verify = false)
    {
        var arguments = $"restore {snapshotId} --repo \"{repoPath}\" --include \"{filePath}\" --target \"{restorePath}\"";
        if (verify)
        {
            arguments += " --verify";
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("恢复文件失败: {Error}", error);
            throw new Exception($"恢复文件失败: {error}");
        }

        _logger.LogInformation("文件恢复成功: {FilePath} 到 {RestorePath}", filePath, restorePath);
    }

    public async Task DeleteSnapshotAsync(string repoPath, string password, string snapshotId)
    {
        // 与批量删除/ListSnapshotsByIdsAsync 一致：只接受 4~64 位十六进制，防止命令行注入
        if (string.IsNullOrWhiteSpace(snapshotId) ||
            !System.Text.RegularExpressions.Regex.IsMatch(snapshotId, "^[a-fA-F0-9]{4,64}$"))
            throw new ArgumentException($"非法的快照 ID: {snapshotId}", nameof(snapshotId));

        var arguments = $"forget {snapshotId} --repo \"{repoPath}\" --prune";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("删除快照失败: {Error}", error);
            throw new Exception($"删除快照失败: {error}");
        }

        // restic forget 对未匹配到任何快照的 ID 也以退出码 0 结束（仅在 stderr 打印
        // "Ignoring ..."），仅凭退出码会把"什么都没删"误判为删除成功。
        // 删后按 ID 回查远端，确认快照确实消失。
        var remaining = await ListSnapshotsByIdsAsync(repoPath, password, new[] { snapshotId });
        if (remaining.Count > 0)
        {
            _logger.LogError("删除快照失败: 快照 {SnapshotId} 仍在远端仓库中（本地缓存可能与远端不同步）", snapshotId);
            throw new Exception("删除快照失败: 快照仍在远端仓库中，本地缓存可能与远端不同步，请先\"同步远端\"后重试");
        }

        _logger.LogInformation("快照删除成功: {SnapshotId}", snapshotId);
    }

    public async Task DeleteSnapshotsAsync(string repoPath, string password, IEnumerable<string> snapshotIds)
    {
        if (snapshotIds == null)
            throw new ArgumentNullException(nameof(snapshotIds));

        // 过滤空 ID 并去重
        var idList = snapshotIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (idList.Count == 0)
            throw new ArgumentException("快照ID不能为空", nameof(snapshotIds));

        // 与 ListSnapshotsByIdsAsync 一致：只接受 4~64 位十六进制，防止命令行注入
        foreach (var id in idList)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-fA-F0-9]{4,64}$"))
                throw new ArgumentException($"非法的快照 ID: {id}", nameof(snapshotIds));
        }

        // restic forget 原生支持一次传入多个快照 ID
        var idArgs = string.Join(" ", idList.Select(id => $"\"{id}\""));
        var arguments = $"forget {idArgs} --repo \"{repoPath}\" --prune";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("批量删除快照失败: {Error}", error);
            throw new Exception($"批量删除快照失败: {error}");
        }

        // 同 DeleteSnapshotAsync：restic 对未匹配的 ID 静默忽略且退出码为 0，删后回查确认全部消失
        var remaining = await ListSnapshotsByIdsAsync(repoPath, password, idList);
        if (remaining.Count > 0)
        {
            var remainIds = string.Join(", ", remaining.Select(s => s.Id));
            _logger.LogError("批量删除快照失败: 以下快照仍在远端仓库中: {SnapshotIds}", remainIds);
            throw new Exception($"批量删除快照失败: {remainIds} 仍在远端仓库中，本地缓存可能与远端不同步，请先\"同步远端\"后重试");
        }

        _logger.LogInformation("批量删除快照成功，共 {Count} 条", idList.Count);
    }

    public async Task PruneSnapshotsAsync(string repoPath, string password, int keepDaily, int keepWeekly, int keepMonthly, int? keepYearly)
    {
        var arguments = $"forget --repo \"{repoPath}\" --keep-daily {keepDaily} --keep-weekly {keepWeekly} --keep-monthly {keepMonthly}";
        if (keepYearly.HasValue && keepYearly.Value > 0)
        {
            arguments += $" --keep-yearly {keepYearly.Value}";
        }
        arguments += " --prune";

        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("清理快照失败: {Error}", error);
            throw new Exception($"清理快照失败: {error}");
        }

        _logger.LogInformation("快照清理成功");
    }

    public async Task<string> CheckRepositoryAsync(string repoPath, string password, string readDataSubset = null)
    {
        var arguments = $"check --repo \"{repoPath}\"";
        if (!string.IsNullOrWhiteSpace(readDataSubset))
        {
            arguments += $" --read-data-subset={readDataSubset.Trim()}";
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("检查仓库失败: {Error}", error);
            throw new Exception($"检查仓库失败: {error}");
        }

        _logger.LogInformation("仓库检查成功");
        return output;
    }

    public async Task<RepositoryStats> GetStatsAsync(string repoPath, string password)
    {
        var arguments = $"stats --repo \"{repoPath}\" --json";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("获取统计信息失败: {Error}", error);
            throw new Exception($"获取统计信息失败: {error}");
        }

        try
        {
            var stats = JsonSerializer.Deserialize<RepositoryStats>(output);
            _logger.LogInformation("成功获取统计信息");
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError("解析统计信息失败: {Error}", ex.Message);
            throw new Exception($"解析统计信息失败: {ex.Message}");
        }
    }

    public async Task<bool> IsRepositoryInitializedAsync(string repoPath, string password)
    {
        var arguments = $"snapshots --repo \"{repoPath}\"";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        // 如果返回码为0，说明仓库已初始化
        var isInitialized = exitCode == 0;
        _logger.LogInformation("仓库初始化状态检查: {IsInitialized}", isInitialized);
        return isInitialized;
    }

    public async Task<string> UnlockRepositoryAsync(string repoPath, string password, bool removeAll = false)
    {
        var arguments = $"unlock --repo \"{repoPath}\"";
        if (removeAll)
        {
            arguments += " --remove-all";
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("解锁仓库失败: {Error}", error);
            throw new Exception($"解锁仓库失败: {error}");
        }

        _logger.LogInformation("仓库解锁成功: {RepoPath}", repoPath);
        return string.IsNullOrWhiteSpace(output) ? "解锁成功" : output;
    }

    public async Task<string> PruneRepositoryAsync(string repoPath, string password)
    {
        var arguments = $"prune --repo \"{repoPath}\"";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("清理仓库失败: {Error}", error);
            throw new Exception($"清理仓库失败: {error}");
        }

        _logger.LogInformation("仓库清理成功: {RepoPath}", repoPath);
        return output;
    }

    public async Task<string> RepairIndexAsync(string repoPath, string password)
    {
        var arguments = $"repair index --repo \"{repoPath}\"";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("修复索引失败: {Error}", error);
            throw new Exception($"修复索引失败: {error}");
        }

        _logger.LogInformation("索引修复成功: {RepoPath}", repoPath);
        return output;
    }

    public async Task<string> RepairSnapshotsAsync(string repoPath, string password)
    {
        var arguments = $"repair snapshots --repo \"{repoPath}\" --forget";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("修复快照失败: {Error}", error);
            throw new Exception($"修复快照失败: {error}");
        }

        _logger.LogInformation("快照修复成功: {RepoPath}", repoPath);
        return output;
    }

    public async Task DumpFileAsync(string repoPath, string password, string snapshotId, string filePath, Stream output)
    {
        var arguments = $"dump {snapshotId} \"{filePath}\" --repo \"{repoPath}\"";
        var (error, exitCode) = await ExecuteResticCommandToStreamAsync(arguments, output, password);

        if (exitCode != 0)
        {
            _logger.LogError("导出文件失败: {Error}", error);
            throw new Exception($"导出文件失败: {error}");
        }

        _logger.LogInformation("文件导出成功: {FilePath}", filePath);
    }

    public async Task<List<FindResult>> FindInSnapshotsAsync(string repoPath, string password, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("搜索模式不能为空", nameof(pattern));

        var arguments = $"find \"{pattern}\" --repo \"{repoPath}\" --json";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("搜索文件失败: {Error}", error);
            throw new Exception($"搜索文件失败: {error}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return new List<FindResult>();
        }

        try
        {
            var results = JsonSerializer.Deserialize<List<FindResult>>(output);
            return (results ?? new List<FindResult>()).Where(r => r.Hits > 0).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError("解析搜索结果失败: {Error}", ex.Message);
            throw new Exception($"解析搜索结果失败: {ex.Message}");
        }
    }

    public async Task SetSnapshotTagsAsync(string repoPath, string password, string snapshotId, List<string> tags)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
            throw new ArgumentException("快照ID不能为空", nameof(snapshotId));

        // --set 传空字符串表示清除全部标签
        var tagValue = tags == null || tags.Count == 0
            ? string.Empty
            : string.Join(",", tags.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)));

        var arguments = $"tag {snapshotId} --repo \"{repoPath}\" --set \"{tagValue}\"";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("设置快照标签失败: {Error}", error);
            throw new Exception($"设置快照标签失败: {error}");
        }

        _logger.LogInformation("快照标签设置成功: {SnapshotId}", snapshotId);
    }

    /// <summary>
    /// 递归对树节点进行排序
    /// </summary>
    /// <param name="nodes">节点列表</param>
    private void SortTreeNodes(List<ResticFileTreeNode> nodes)
    {
        // 先对当前层节点按名称排序（目录优先，然后是文件）
        nodes.Sort((a, b) =>
        {
            // 目录优先于文件
            if (a.Type == "dir" && b.Type != "dir")
                return -1;
            if (a.Type != "dir" && b.Type == "dir")
                return 1;

            // 相同类型按名称排序
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        // 递归对子节点排序
        foreach (var node in nodes)
        {
            if (node.Children.Any())
            {
                SortTreeNodes(node.Children);
            }
        }
    }

    private async Task<(string output, string error, int exitCode)> ExecuteResticCommandAsync(string arguments, params string[] inputs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // 输入参数
        if (inputs != null && inputs.Length > 0)
        {
            foreach (var input in inputs)
            {
                await process.StandardInput.WriteLineAsync(input);
            }
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return (output, error, process.ExitCode);
    }


    private async Task<(List<string> outputLines, List<string> errorLines, int exitCode)> ExecuteResticCommandWithLineOutputAsync(string arguments, params string[] inputs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // 输入参数
        if (inputs != null && inputs.Length > 0)
        {
            foreach (var input in inputs)
            {
                await process.StandardInput.WriteLineAsync(input);
            }
            process.StandardInput.Close();
        }

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                outputLines.Add(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorLines.Add(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (outputLines, errorLines, process.ExitCode);
    }

    /// <summary>
    /// 执行 restic 命令并把标准输出的原始字节流复制到目标流（用于 dump 等二进制输出，
    /// 避免 UTF-8 解码破坏二进制内容）。返回标准错误文本与退出码。
    /// </summary>
    private async Task<(string error, int exitCode)> ExecuteResticCommandToStreamAsync(string arguments, Stream output, params string[] inputs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // 输入参数（密码）
        if (inputs != null && inputs.Length > 0)
        {
            foreach (var input in inputs)
            {
                await process.StandardInput.WriteLineAsync(input);
            }
            process.StandardInput.Close();
        }

        // 并发读取标准错误，避免缓冲区写满导致死锁
        var errorTask = process.StandardError.ReadToEndAsync();
        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(output);

        await Task.WhenAll(copyTask, errorTask);
        await process.WaitForExitAsync();

        return (await errorTask, process.ExitCode);
    }
}