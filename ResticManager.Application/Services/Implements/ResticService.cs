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

    public async Task<BackupResult> CreateBackupAsync(string sourcePath, string repoPath, string password, string includePatterns = null, string excludePatterns = null)
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

    public async Task RestoreSnapshotAsync(string repoPath, string password, string snapshotId, string targetPath)
    {
        var arguments = $"restore {snapshotId} --repo \"{repoPath}\" --target \"{targetPath}\"";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("恢复快照失败: {Error}", error);
            throw new Exception($"恢复快照失败: {error}");
        }

        _logger.LogInformation("快照恢复成功: {SnapshotId} 到 {TargetPath}", snapshotId, targetPath);
    }

    public async Task RestoreFileAsync(string repoPath, string password, string snapshotId, string filePath, string restorePath)
    {
        var arguments = $"restore {snapshotId} --repo \"{repoPath}\" --include \"{filePath}\" --target \"{restorePath}\"";
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
        var arguments = $"forget {snapshotId} --repo \"{repoPath}\" --prune";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("删除快照失败: {Error}", error);
            throw new Exception($"删除快照失败: {error}");
        }

        _logger.LogInformation("快照删除成功: {SnapshotId}", snapshotId);
    }

    public async Task PruneSnapshotsAsync(string repoPath, string password, int keepDaily, int keepWeekly, int keepMonthly)
    {
        var arguments = $"forget --repo \"{repoPath}\" --keep-daily {keepDaily} --keep-weekly {keepWeekly} --keep-monthly {keepMonthly} --prune";
        var (output, error, exitCode) = await ExecuteResticCommandAsync(arguments, password);

        if (exitCode != 0)
        {
            _logger.LogError("清理快照失败: {Error}", error);
            throw new Exception($"清理快照失败: {error}");
        }

        _logger.LogInformation("快照清理成功");
    }

    public async Task<string> CheckRepositoryAsync(string repoPath, string password)
    {
        var arguments = $"check --repo \"{repoPath}\"";
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
}