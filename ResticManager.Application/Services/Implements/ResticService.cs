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
using System.Text.RegularExpressions;

namespace ResticManager.Application.Services.Implements;

public class ResticService : IResticService
{
    private static readonly Regex SnapshotIdRegex = new(@"^[a-fA-F0-9]{4,64}$", RegexOptions.Compiled);
    private static readonly Regex ReadDataSubsetRegex = new(@"^(\d{1,3}%|\d+/\d+|\d+[KMGTP]?)$", RegexOptions.Compiled);
    private static readonly Regex ExcludeLargerThanRegex = new(@"^\d+[kKmMgGtTpP]?$", RegexOptions.Compiled);
    private static readonly Regex PlainDiffChangeRegex = new(@"^([+\-MUT?]{1,2})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex PlainDiffFilesRegex = new(@"^Files:\s+(\d+)\s+new,\s+(\d+)\s+removed,\s+(\d+)\s+changed", RegexOptions.Compiled);
    private static readonly Regex PlainDiffDirsRegex = new(@"^Dirs:\s+(\d+)\s+new,\s+(\d+)\s+removed", RegexOptions.Compiled);
    private static readonly Regex PlainDiffOthersRegex = new(@"^Others:\s+(\d+)\s+new,\s+(\d+)\s+removed", RegexOptions.Compiled);
    private static readonly Regex PlainDiffDataBlobsRegex = new(@"^Data Blobs:\s+(\d+)\s+new,\s+(\d+)\s+removed", RegexOptions.Compiled);
    private static readonly Regex PlainDiffTreeBlobsRegex = new(@"^Tree Blobs:\s+(\d+)\s+new,\s+(\d+)\s+removed", RegexOptions.Compiled);

    private readonly string _resticPath;
    private readonly ILogger<ResticService> _logger;

    public ResticService(IConfiguration configuration, ILogger<ResticService> logger)
    {
        _resticPath = configuration["Restic:Path"];
        _resticPath = string.IsNullOrEmpty(_resticPath) ? "restic" : _resticPath;
        _logger = logger;
    }

    private static void ValidateSnapshotId(string snapshotId, string paramName)
    {
        if (string.IsNullOrWhiteSpace(snapshotId) ||
            (snapshotId != "latest" && !SnapshotIdRegex.IsMatch(snapshotId)))
        {
            throw new ArgumentException($"非法的快照 ID: {snapshotId}", paramName);
        }
    }

    public async Task InitializeRepositoryAsync(Repository repository)
    {
        var args = new List<string> { "init", "--repo", repository.Path };
        // 使用标准输入流式传递密码（需要两次输入：设置密码和确认密码）
        var (output, error, exitCode) = await ExecuteResticCommandAsync(
            repository,
            args,
            new[] { repository.Password, repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("初始化仓库失败: {Error}", error);
            throw new Exception($"初始化仓库失败: {error}");
        }

        _logger.LogInformation("仓库初始化成功: {RepoPath}", repository.Path);
    }

    public async Task<BackupResult> CreateBackupAsync(string sourcePath, Repository repository, string includePatterns = null, string excludePatterns = null, BackupOptions options = null)
    {
        var args = new List<string> { "backup", sourcePath, "--repo", repository.Path, "--json" };

        if (!string.IsNullOrEmpty(includePatterns))
        {
            foreach (var pattern in includePatterns.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)))
            {
                args.Add("--include");
                args.Add(pattern);
            }
        }

        if (!string.IsNullOrEmpty(excludePatterns))
        {
            foreach (var pattern in excludePatterns.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)))
            {
                args.Add("--exclude");
                args.Add(pattern);
            }
        }

        if (options != null)
        {
            if (options.Tags != null)
            {
                foreach (var tag in options.Tags.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    args.Add("--tag");
                    args.Add(tag.Trim());
                }
            }
            if (options.ExcludeCaches)
            {
                args.Add("--exclude-caches");
            }
            if (!string.IsNullOrWhiteSpace(options.ExcludeLargerThan))
            {
                var trimmed = options.ExcludeLargerThan.Trim();
                if (!ExcludeLargerThanRegex.IsMatch(trimmed))
                    throw new ArgumentException($"非法的排除文件大小参数: {options.ExcludeLargerThan}", nameof(options));

                args.Add("--exclude-larger-than");
                args.Add(trimmed);
            }
            if (options.OneFileSystem)
            {
                args.Add("--one-file-system");
            }
            if (options.DryRun)
            {
                args.Add("--dry-run");
            }
        }

        var (outputLines, errorLines, exitCode) = await ExecuteResticCommandWithLineOutputAsync(
            repository,
            args,
            new[] { repository.Password });

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

    public async Task<List<SnapshotInfo>> ListSnapshotsAsync(Repository repository)
    {
        var args = new List<string> { "snapshots", "--repo", repository.Path, "--json" };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

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

    public async Task<List<SnapshotInfo>> ListSnapshotsByIdsAsync(Repository repository, IEnumerable<string> ids)
    {
        if (ids == null)
            throw new ArgumentNullException(nameof(ids));

        // 过滤掉空 ID
        var idList = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (idList.Count == 0)
            return new List<SnapshotInfo>();

        foreach (var id in idList)
        {
            ValidateSnapshotId(id, nameof(ids));
        }

        // restic 0.18.1: `snapshots --repo <path> --json <id1> <id2> ...` 支持按 ID 过滤
        var args = new List<string> { "snapshots", "--repo", repository.Path, "--json" };
        args.AddRange(idList);

        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

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

    public async Task<List<ResticFileInfo>> ListFilesInSnapshotAsync(Repository repository, string snapshotId, string dirPath)
    {
        ValidateSnapshotId(snapshotId, nameof(snapshotId));

        var formattedDirPath = string.IsNullOrWhiteSpace(dirPath)
            ? "/"
            : dirPath.Replace(":\\", "/").Replace("\\", "/");
        if (!formattedDirPath.StartsWith("/"))
        {
            formattedDirPath = $"/{formattedDirPath}";
        }

        // 移除末尾的斜杠（根目录除外）
        if (formattedDirPath.Length > 1 && formattedDirPath.EndsWith("/"))
        {
            formattedDirPath = formattedDirPath.TrimEnd('/');
        }

        var args = new List<string> { "-r", repository.Path, "ls", snapshotId, formattedDirPath, "--json" };
        _logger.LogDebug("restic 参数: {Arguments}", string.Join(" ", args));

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        var (outputLines, errorLines, exitCode) = await ExecuteResticCommandWithLineOutputAsync(
            repository,
            args,
            new[] { repository.Password });

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
        _logger.LogInformation("成功获取文件列表，共 {Count} 个文件,耗时：{ElapsedMs} ms", files.Count, stopwatch.ElapsedMilliseconds);

        // 如果返回的是目录本身的信息，我们需要获取该目录下的文件
        if (files.Count(f => !string.IsNullOrEmpty(f.Type)) == 0 && files.Count == 1)
        {
            // 重新执行命令获取目录内容
            var targetPath = files.FirstOrDefault()?.Paths?.FirstOrDefault() ?? formattedDirPath;
            var retryArgs = new List<string> { "-r", repository.Path, "ls", snapshotId, targetPath, "--json" };
            var (retryOutputLines, retryErrorLines, retryExitCode) = await ExecuteResticCommandWithLineOutputAsync(
                repository,
                retryArgs,
                new[] { repository.Password });

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

    public async Task<SnapshotDiffResult> DiffSnapshotsAsync(Repository repository, string snapshotId1, string snapshotId2)
    {
        ValidateSnapshotId(snapshotId1, nameof(snapshotId1));
        ValidateSnapshotId(snapshotId2, nameof(snapshotId2));

        // 先尝试使用 --json 参数获取 JSON 格式输出
        var args = new List<string> { "diff", snapshotId1, snapshotId2, "--repo", repository.Path, "--json" };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });
        var usedJson = true;

        // 如果不支持 --json 参数，则降级为纯文本输出
        if (exitCode != 0 && error.Contains("--json", StringComparison.OrdinalIgnoreCase))
        {
            usedJson = false;
            args = new List<string> { "diff", snapshotId1, snapshotId2, "--repo", repository.Path };
            (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });
        }

        if (exitCode != 0)
        {
            _logger.LogError("比较快照差异失败: {Error}", error);
            throw new Exception($"比较快照差异失败: {error}");
        }

        var diffResult = usedJson
            ? ParseJsonDiffOutput(output)
            : ParsePlainTextDiffOutput(output, snapshotId1, snapshotId2);

        _logger.LogInformation("快照差异比较成功: {SnapshotId1} vs {SnapshotId2}", snapshotId1, snapshotId2);
        return diffResult;
    }

    private SnapshotDiffResult ParseJsonDiffOutput(string output)
    {
        var diffResult = new SnapshotDiffResult();

        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (root.TryGetProperty("message_type", out var messageTypeElement))
                {
                    var messageType = messageTypeElement.GetString();
                    if (messageType == "change")
                    {
                        var change = JsonSerializer.Deserialize<SnapshotDiffChange>(line);
                        if (change != null)
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

        return diffResult;
    }

    private static SnapshotDiffResult ParsePlainTextDiffOutput(string output, string snapshotId1, string snapshotId2)
    {
        var diffResult = new SnapshotDiffResult();
        SnapshotDiffStatistics stats = null;

        SnapshotDiffStatistics EnsureStats()
        {
            stats ??= new SnapshotDiffStatistics
            {
                MessageType = "statistics",
                SourceSnapshot = snapshotId1,
                TargetSnapshot = snapshotId2,
                Added = new SnapshotDiffAddRemoveStats(),
                Removed = new SnapshotDiffAddRemoveStats()
            };
            return stats;
        }

        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var trimmed = line.Trim();

            var filesMatch = PlainDiffFilesRegex.Match(trimmed);
            if (filesMatch.Success)
            {
                var s = EnsureStats();
                s.Added.Files = int.Parse(filesMatch.Groups[1].Value);
                s.Removed.Files = int.Parse(filesMatch.Groups[2].Value);
                s.ChangedFiles = int.Parse(filesMatch.Groups[3].Value);
                continue;
            }

            var dirsMatch = PlainDiffDirsRegex.Match(trimmed);
            if (dirsMatch.Success)
            {
                var s = EnsureStats();
                s.Added.Dirs = int.Parse(dirsMatch.Groups[1].Value);
                s.Removed.Dirs = int.Parse(dirsMatch.Groups[2].Value);
                continue;
            }

            var othersMatch = PlainDiffOthersRegex.Match(trimmed);
            if (othersMatch.Success)
            {
                var s = EnsureStats();
                s.Added.Others = int.Parse(othersMatch.Groups[1].Value);
                s.Removed.Others = int.Parse(othersMatch.Groups[2].Value);
                continue;
            }

            var dataBlobsMatch = PlainDiffDataBlobsRegex.Match(trimmed);
            if (dataBlobsMatch.Success)
            {
                var s = EnsureStats();
                s.Added.DataBlobs = int.Parse(dataBlobsMatch.Groups[1].Value);
                s.Removed.DataBlobs = int.Parse(dataBlobsMatch.Groups[2].Value);
                continue;
            }

            var treeBlobsMatch = PlainDiffTreeBlobsRegex.Match(trimmed);
            if (treeBlobsMatch.Success)
            {
                var s = EnsureStats();
                s.Added.TreeBlobs = int.Parse(treeBlobsMatch.Groups[1].Value);
                s.Removed.TreeBlobs = int.Parse(treeBlobsMatch.Groups[2].Value);
                continue;
            }

            var changeMatch = PlainDiffChangeRegex.Match(line);
            if (changeMatch.Success)
            {
                var path = changeMatch.Groups[2].Value.Trim();
                if (path.StartsWith('/'))
                {
                    diffResult.Changes.Add(new SnapshotDiffChange
                    {
                        MessageType = "change",
                        Modifier = changeMatch.Groups[1].Value,
                        Path = path
                    });
                }
            }
        }

        diffResult.Statistics = stats;
        return diffResult;
    }

    public async Task RestoreSnapshotAsync(Repository repository, string snapshotId, string targetPath, bool verify = false)
    {
        ValidateSnapshotId(snapshotId, nameof(snapshotId));

        var args = new List<string> { "restore", snapshotId, "--repo", repository.Path, "--target", targetPath };
        if (verify)
        {
            args.Add("--verify");
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("恢复快照失败: {Error}", error);
            throw new Exception($"恢复快照失败: {error}");
        }

        _logger.LogInformation("快照恢复成功: {SnapshotId} 到 {TargetPath}", snapshotId, targetPath);
    }

    public async Task RestoreFileAsync(Repository repository, string snapshotId, string filePath, string restorePath, bool verify = false)
    {
        ValidateSnapshotId(snapshotId, nameof(snapshotId));

        var args = new List<string>
        {
            "restore", snapshotId,
            "--repo", repository.Path,
            "--include", filePath,
            "--target", restorePath
        };
        if (verify)
        {
            args.Add("--verify");
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("恢复文件失败: {Error}", error);
            throw new Exception($"恢复文件失败: {error}");
        }

        _logger.LogInformation("文件恢复成功: {FilePath} 到 {RestorePath}", filePath, restorePath);
    }

    public async Task DeleteSnapshotAsync(Repository repository, string snapshotId)
    {
        ValidateSnapshotId(snapshotId, nameof(snapshotId));

        var args = new List<string> { "forget", snapshotId, "--repo", repository.Path, "--prune" };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("删除快照失败: {Error}", error);
            throw new Exception($"删除快照失败: {error}");
        }

        // restic forget 对未匹配到任何快照的 ID 也以退出码 0 结束（仅在 stderr 打印
        // "Ignoring ..."），仅凭退出码会把"什么都没删"误判为删除成功。
        // 删后按 ID 回查远端，确认快照确实消失。
        var remaining = await ListSnapshotsByIdsAsync(repository, new[] { snapshotId });
        if (remaining.Count > 0)
        {
            _logger.LogError("删除快照失败: 快照 {SnapshotId} 仍在远端仓库中（本地缓存可能与远端不同步）", snapshotId);
            throw new Exception("删除快照失败: 快照仍在远端仓库中，本地缓存可能与远端不同步，请先\"同步远端\"后重试");
        }

        _logger.LogInformation("快照删除成功: {SnapshotId}", snapshotId);
    }

    public async Task DeleteSnapshotsAsync(Repository repository, IEnumerable<string> snapshotIds)
    {
        if (snapshotIds == null)
            throw new ArgumentNullException(nameof(snapshotIds));

        // 过滤空 ID 并去重
        var idList = snapshotIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (idList.Count == 0)
            throw new ArgumentException("快照ID不能为空", nameof(snapshotIds));

        foreach (var id in idList)
        {
            ValidateSnapshotId(id, nameof(snapshotIds));
        }

        // restic forget 原生支持一次传入多个快照 ID
        var args = new List<string> { "forget" };
        args.AddRange(idList);
        args.Add("--repo");
        args.Add(repository.Path);
        args.Add("--prune");

        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("批量删除快照失败: {Error}", error);
            throw new Exception($"批量删除快照失败: {error}");
        }

        // 同 DeleteSnapshotAsync：restic 对未匹配的 ID 静默忽略且退出码为 0，删后回查确认全部消失
        var remaining = await ListSnapshotsByIdsAsync(repository, idList);
        if (remaining.Count > 0)
        {
            var remainIds = string.Join(", ", remaining.Select(s => s.Id));
            _logger.LogError("批量删除快照失败: 以下快照仍在远端仓库中: {SnapshotIds}", remainIds);
            throw new Exception($"批量删除快照失败: {remainIds} 仍在远端仓库中，本地缓存可能与远端不同步，请先\"同步远端\"后重试");
        }

        _logger.LogInformation("批量删除快照成功，共 {Count} 条", idList.Count);
    }

    public async Task PruneSnapshotsAsync(Repository repository, int? keepDaily, int? keepWeekly, int? keepMonthly, int? keepYearly)
    {
        var args = new List<string> { "forget", "--repo", repository.Path };
        var hasRetention = false;

        if (keepDaily.HasValue && keepDaily.Value > 0)
        {
            args.Add("--keep-daily");
            args.Add(keepDaily.Value.ToString());
            hasRetention = true;
        }
        if (keepWeekly.HasValue && keepWeekly.Value > 0)
        {
            args.Add("--keep-weekly");
            args.Add(keepWeekly.Value.ToString());
            hasRetention = true;
        }
        if (keepMonthly.HasValue && keepMonthly.Value > 0)
        {
            args.Add("--keep-monthly");
            args.Add(keepMonthly.Value.ToString());
            hasRetention = true;
        }
        if (keepYearly.HasValue && keepYearly.Value > 0)
        {
            args.Add("--keep-yearly");
            args.Add(keepYearly.Value.ToString());
            hasRetention = true;
        }

        if (!hasRetention)
        {
            _logger.LogInformation("未配置任何有效的快照保留策略，跳过快照清理");
            return;
        }

        args.Add("--prune");

        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("清理快照失败: {Error}", error);
            throw new Exception($"清理快照失败: {error}");
        }

        _logger.LogInformation("快照清理成功");
    }

    public async Task<string> CheckRepositoryAsync(Repository repository, string readDataSubset = null)
    {
        var args = new List<string> { "check", "--repo", repository.Path };
        if (!string.IsNullOrWhiteSpace(readDataSubset))
        {
            var trimmed = readDataSubset.Trim();
            if (!ReadDataSubsetRegex.IsMatch(trimmed))
                throw new ArgumentException($"非法的数据校验子集参数: {readDataSubset}", nameof(readDataSubset));

            args.Add($"--read-data-subset={trimmed}");
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("检查仓库失败: {Error}", error);
            throw new Exception($"检查仓库失败: {error}");
        }

        _logger.LogInformation("仓库检查成功");
        return output;
    }

    public async Task<RepositoryStats> GetStatsAsync(Repository repository)
    {
        var args = new List<string> { "stats", "--repo", repository.Path, "--json" };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

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

    public async Task<bool> IsRepositoryInitializedAsync(Repository repository)
    {
        var args = new List<string> { "snapshots", "--repo", repository.Path };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode == 0)
        {
            _logger.LogInformation("仓库初始化状态检查: True");
            return true;
        }

        var err = error ?? string.Empty;
        if (err.Contains("Is there a repository", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("unable to open config file", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("no such file", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("仓库初始化状态检查: False");
            return false;
        }

        _logger.LogError("检查仓库初始化状态失败: {Error}", error);
        throw new Exception($"检查仓库初始化状态失败: {error}");
    }

    public async Task<string> UnlockRepositoryAsync(Repository repository, bool removeAll = false)
    {
        var args = new List<string> { "unlock", "--repo", repository.Path };
        if (removeAll)
        {
            args.Add("--remove-all");
        }
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("解锁仓库失败: {Error}", error);
            throw new Exception($"解锁仓库失败: {error}");
        }

        _logger.LogInformation("仓库解锁成功: {RepoPath}", repository.Path);
        return string.IsNullOrWhiteSpace(output) ? "解锁成功" : output;
    }

    public async Task<string> PruneRepositoryAsync(Repository repository)
    {
        var args = new List<string> { "prune", "--repo", repository.Path };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("清理仓库失败: {Error}", error);
            throw new Exception($"清理仓库失败: {error}");
        }

        _logger.LogInformation("仓库清理成功: {RepoPath}", repository.Path);
        return output;
    }

    public async Task<string> RepairIndexAsync(Repository repository)
    {
        var args = new List<string> { "repair", "index", "--repo", repository.Path };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("修复索引失败: {Error}", error);
            throw new Exception($"修复索引失败: {error}");
        }

        _logger.LogInformation("索引修复成功: {RepoPath}", repository.Path);
        return output;
    }

    public async Task<string> RepairSnapshotsAsync(Repository repository)
    {
        var args = new List<string> { "repair", "snapshots", "--repo", repository.Path, "--forget" };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("修复快照失败: {Error}", error);
            throw new Exception($"修复快照失败: {error}");
        }

        _logger.LogInformation("快照修复成功: {RepoPath}", repository.Path);
        return output;
    }

    public async Task DumpFileAsync(Repository repository, string snapshotId, string filePath, Stream output)
    {
        ValidateSnapshotId(snapshotId, nameof(snapshotId));

        var args = new List<string> { "dump", snapshotId, filePath, "--repo", repository.Path };
        var (error, exitCode) = await ExecuteResticCommandToStreamAsync(repository, args, output, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("导出文件失败: {Error}", error);
            throw new Exception($"导出文件失败: {error}");
        }

        _logger.LogInformation("文件导出成功: {FilePath}", filePath);
    }

    public async Task<List<FindResult>> FindInSnapshotsAsync(Repository repository, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("搜索模式不能为空", nameof(pattern));

        var args = new List<string> { "find", pattern, "--repo", repository.Path, "--json" };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

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

    public async Task SetSnapshotTagsAsync(Repository repository, string snapshotId, List<string> tags)
    {
        ValidateSnapshotId(snapshotId, nameof(snapshotId));

        // --set 传空字符串表示清除全部标签
        var tagValue = tags == null || tags.Count == 0
            ? string.Empty
            : string.Join(",", tags.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)));

        var args = new List<string> { "tag", snapshotId, "--repo", repository.Path, "--set", tagValue };
        var (output, error, exitCode) = await ExecuteResticCommandAsync(repository, args, new[] { repository.Password });

        if (exitCode != 0)
        {
            _logger.LogError("设置快照标签失败: {Error}", error);
            throw new Exception($"设置快照标签失败: {error}");
        }

        _logger.LogInformation("快照标签设置成功: {SnapshotId}", snapshotId);
    }

    private ProcessStartInfo BuildStartInfo(Repository repository, IEnumerable<string> arguments, bool redirectUtf8Output = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _resticPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (redirectUtf8Output)
        {
            startInfo.StandardOutputEncoding = Encoding.UTF8;
        }

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        ResticEnvironmentBuilder.Apply(startInfo, repository);
        return startInfo;
    }

    private static async Task WriteInputsAsync(Process process, IEnumerable<string> inputs, CancellationToken cancellationToken)
    {
        try
        {
            if (inputs != null)
            {
                foreach (var input in inputs)
                {
                    await process.StandardInput.WriteLineAsync((input ?? string.Empty).AsMemory(), cancellationToken);
                }
            }
        }
        catch (IOException)
        {
            // 子进程可能已提前退出（Broken Pipe），忽略写入异常以保留真实 stderr
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch
            {
                // 忽略关闭异常
            }
        }
    }

    private static async Task<List<string>> ReadAllLinesAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            if (line.Length > 0)
                lines.Add(line);
        }
        return lines;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 进程可能已退出或不可终止，忽略
        }
    }

    private async Task<(string output, string error, int exitCode)> ExecuteResticCommandAsync(
        Repository repository,
        IEnumerable<string> arguments,
        IEnumerable<string> inputs = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = BuildStartInfo(repository, arguments) };
        process.Start();

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await WriteInputsAsync(process, inputs, cancellationToken);

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            return (await outputTask, await errorTask, process.ExitCode);
        }
        catch
        {
            TryKillProcess(process);
            throw;
        }
    }

    private async Task<(List<string> outputLines, List<string> errorLines, int exitCode)> ExecuteResticCommandWithLineOutputAsync(
        Repository repository,
        IEnumerable<string> arguments,
        IEnumerable<string> inputs = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = BuildStartInfo(repository, arguments) };
        process.Start();

        try
        {
            var outputTask = ReadAllLinesAsync(process.StandardOutput, cancellationToken);
            var errorTask = ReadAllLinesAsync(process.StandardError, cancellationToken);

            await WriteInputsAsync(process, inputs, cancellationToken);

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            return (await outputTask, await errorTask, process.ExitCode);
        }
        catch
        {
            TryKillProcess(process);
            throw;
        }
    }

    /// <summary>
    /// 执行 restic 命令并把标准输出的原始字节流复制到目标流（用于 dump 等二进制输出，
    /// 避免 UTF-8 解码破坏二进制内容）。返回标准错误文本与退出码。
    /// </summary>
    private async Task<(string error, int exitCode)> ExecuteResticCommandToStreamAsync(
        Repository repository,
        IEnumerable<string> arguments,
        Stream output,
        IEnumerable<string> inputs = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = BuildStartInfo(repository, arguments, redirectUtf8Output: false) };
        process.Start();

        try
        {
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);

            await WriteInputsAsync(process, inputs, cancellationToken);

            await Task.WhenAll(copyTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            return (await errorTask, process.ExitCode);
        }
        catch
        {
            TryKillProcess(process);
            throw;
        }
    }
}