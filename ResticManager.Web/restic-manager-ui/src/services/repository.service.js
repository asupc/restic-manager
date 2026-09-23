import api from "./api";

// 说明：所有会访问 restic 仓库的接口（初始化/统计/同步/恢复/删除/prune/校验等）
// 均为后台异步任务——提交后立即返回 { taskId }，
// 用 src/utils/task-poller.js 的 pollTask(taskId) 轮询到终态，
// 结果在 task.resultJson（JSON 字符串），失败原因在 task.error。
export const repositoryService = {
  async getRepository(id) {
    const response = await api.get(`/repository/${id}`);
    return response.data;
  },

  // 获取所有仓库
  async getRepositories() {
    const response = await api.get("/repository");
    return response.data;
  },

  // 创建仓库
  async createRepository(repositoryData) {
    const response = await api.post("/repository", repositoryData);
    return response.data;
  },

  // 更新仓库
  async updateRepository(id, repositoryData) {
    const response = await api.put(`/repository/${id}`, repositoryData);
    return response.data;
  },

  // 删除仓库
  async deleteRepository(id) {
    await api.delete(`/repository/${id}`);
  },

  // 初始化仓库（异步任务，返回 { taskId }）
  async initializeRepository(id) {
    const response = await api.post(`/repository/${id}/initialize`);
    return response.data;
  },

  // 获取仓库统计信息（异步任务，返回 { taskId }，结果为 RepositoryStats）
  async getRepositoryStats(id) {
    const response = await api.get(`/repository/${id}/stats`);
    return response.data;
  },

  // 获取仓库快照
  async getSnapshots(repositoryId) {
    const response = await api.get(`/repository/${repositoryId}/snapshots`);
    return response.data;
  },

  // 从远端仓库全量同步快照元数据到本地缓存（异步任务，返回 { taskId }，结果为同步统计；
  // 用于首次部署、跨设备导入等场景）
  async syncSnapshots(repositoryId) {
    const response = await api.post(`/repository/${repositoryId}/snapshots/sync`);
    return response.data;
  },

  // 恢复快照（异步任务，返回 { taskId }）
  async restoreSnapshot(repositoryId, snapshotId, targetPath, verify = false) {
    const response = await api.post(
      `/repository/${repositoryId}/snapshots/${snapshotId}/restore`,
      {
        targetPath,
        verify,
      }
    );
    return response.data;
  },

  // 恢复文件（异步任务，返回 { taskId }）
  async restoreFile(repositoryId, snapshotId, filePath, targetPath, verify = false) {
    const response = await api.post(
      `/repository/${repositoryId}/files/restore`,
      {
        targetPath,
        snapshotId,
        filePath,
        verify,
      }
    );
    return response.data;
  },

  // 删除快照（异步任务，返回 { taskId }）
  async deleteSnapshot(repositoryId, snapshotId) {
    const response = await api.delete(
      `/repository/${repositoryId}/snapshots/${snapshotId}`
    );
    return response.data;
  },

  // 批量删除快照（异步任务，返回 { taskId }，结果为删除/标记条数）
  async batchDeleteSnapshots(repositoryId, snapshotIds) {
    const response = await api.post(
      `/repository/${repositoryId}/snapshots/batch-delete`,
      { snapshotIds }
    );
    return response.data;
  },

  // 验证仓库（异步任务，返回 { taskId }，结果为检查记录摘要）
  async validateRepository(id) {
    const response = await api.post(`/repository/${id}/validate`);
    return response.data;
  },

  //   /**
  //    * 获取快照中的文件列表（树形结构）
  //    * @param {number} id 仓库ID
  //    * @param {string} snapshotId 快照ID
  //    * @returns {Promise<Array>} 文件树形结构列表
  //    */
  //   async getSnapshotFilesTree(id, snapshotId) {
  //     const response = await api.get(
  //       `/Repository/${id}/snapshots/${snapshotId}/subdirectory-files-tree`
  //     );
  //     return response.data;
  //   },

  /**
   * 获取快照中指定子目录的文件列表（树形结构，异步任务，返回 { taskId }，结果为节点数组）
   * @param {number} id 仓库ID
   * @param {string} snapshotId 快照ID
   * @param {string} path 子目录路径
   * @returns {Promise<Object>} { taskId }
   */
  async getSnapshotSubdirectoryFilesTree(id, snapshotId, path) {
    const response = await api.get(
      `/Repository/${id}/snapshots/${snapshotId}/subdirectory-files-tree?path=${path}`
    );
    return response.data;
  },

  /**
   * 比较快照之间的差异（异步任务，返回 { taskId }）
   * @param {number} id 仓库ID
   * @param {string} snapshotId1 第一个快照ID
   * @param {string} snapshotId2 第二个快照ID
   * @returns {Promise<Object>} { taskId }，结果为差异对象（含 changes 和 statistics）
   */
  async diffSnapshots(id, snapshotId1, snapshotId2) {
    const response = await api.get(
      `/Repository/${id}/snapshots/${snapshotId1}/diff/${snapshotId2}`
    );
    return response.data;
  },

  // 手动执行仓库健康检查（异步任务，返回 { taskId }）
  async checkRepository(id) {
    const response = await api.post(`/repository/${id}/check`);
    return response.data;
  },

  // 获取仓库健康检查历史
  async getHealthRecords(id, limit = 20) {
    const response = await api.get(`/repository/${id}/health-records?limit=${limit}`);
    return response.data;
  },

  // 获取全部仓库健康概览
  async getHealthOverview() {
    const response = await api.get(`/repository/health-overview`);
    return response.data;
  },

  // 解锁仓库，清理陈旧锁（异步任务，返回 { taskId }，结果含 output）
  async unlockRepository(id, removeAll = false) {
    const response = await api.post(`/repository/${id}/unlock`, { removeAll });
    return response.data;
  },

  // 清理未引用数据（prune，异步任务，返回 { taskId }，结果含 output）
  async pruneRepository(id) {
    const response = await api.post(`/repository/${id}/prune`);
    return response.data;
  },

  // 修复索引（异步任务，返回 { taskId }，结果含 output）
  async repairIndex(id) {
    const response = await api.post(`/repository/${id}/repair-index`);
    return response.data;
  },

  // 修复损坏的快照（异步任务，返回 { taskId }，结果含 output）
  async repairSnapshots(id) {
    const response = await api.post(`/repository/${id}/repair-snapshots`);
    return response.data;
  },

  // 深度校验（读取部分数据包，异步任务，返回 { taskId }，结果含 output）
  async deepCheckRepository(id, subset = "5%") {
    const response = await api.post(
      `/repository/${id}/deep-check?subset=${encodeURIComponent(subset)}`
    );
    return response.data;
  },

  // 跨快照搜索文件（异步任务，返回 { taskId }，结果为 FindResult 数组）
  async findInSnapshots(id, pattern) {
    const response = await api.get(
      `/repository/${id}/find?pattern=${encodeURIComponent(pattern)}`
    );
    return response.data;
  },

  // 设置（覆盖）快照标签（异步任务，返回 { taskId }）
  async setSnapshotTags(id, snapshotId, tags) {
    const response = await api.put(
      `/repository/${id}/snapshots/${snapshotId}/tags`,
      { tags }
    );
    return response.data;
  },

  // 下载快照中的单个文件（浏览器触发下载）
  async downloadSnapshotFile(id, snapshotId, path) {
    const response = await api.get(
      `/repository/${id}/snapshots/${snapshotId}/download?path=${encodeURIComponent(path)}`,
      { responseType: "blob" }
    );
    const fileName = path.split("/").filter(Boolean).pop() || "download";
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", fileName);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  },
};