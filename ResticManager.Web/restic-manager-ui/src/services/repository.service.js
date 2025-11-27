import api from "./api";

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

  // 初始化仓库
  async initializeRepository(id) {
    const response = await api.post(`/repository/${id}/initialize`);
    return response.data;
  },

  // 获取仓库统计信息
  async getRepositoryStats(id) {
    const response = await api.get(`/repository/${id}/stats`);
    return response.data;
  },

  // 获取仓库快照
  async getSnapshots(repositoryId) {
    const response = await api.get(`/repository/${repositoryId}/snapshots`);
    return response.data;
  },

  // 获取快照文件
  async getSnapshotFiles(repositoryId, snapshotId) {
    const response = await api.get(
      `/repository/${repositoryId}/snapshots/${snapshotId}/files`
    );
    return response.data;
  },

  // 恢复快照
  async restoreSnapshot(repositoryId, snapshotId, targetPath) {
    const response = await api.post(
      `/repository/${repositoryId}/snapshots/${snapshotId}/restore`,
      {
        targetPath,
      }
    );
    return response.data;
  },

  // 恢复文件
  async restoreFile(repositoryId, snapshotId, filePath, targetPath) {
    const response = await api.post(
      `/repository/${repositoryId}/files/restore`,
      {
        targetPath,
        snapshotId,
        filePath,
      }
    );
    return response.data;
  },

  // 删除快照
  async deleteSnapshot(repositoryId, snapshotId) {
    const response = await api.delete(
      `/repository/${repositoryId}/snapshots/${snapshotId}`
    );
    return response.data;
  },

  // 验证仓库
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
   * 获取快照中指定子目录的文件列表（树形结构）
   * @param {number} id 仓库ID
   * @param {string} snapshotId 快照ID
   * @param {string} path 子目录路径
   * @returns {Promise<Array>} 文件树形结构列表
   */
  async getSnapshotSubdirectoryFilesTree(id, snapshotId, path) {
    const response = await api.get(
      `/Repository/${id}/snapshots/${snapshotId}/subdirectory-files-tree?path=${path}`
    );
    return response.data;
  },

  /**
   * 比较快照之间的差异
   * @param {number} id 仓库ID
   * @param {string} snapshotId1 第一个快照ID
   * @param {string} snapshotId2 第二个快照ID
   * @returns {Promise<Object>} 差异结果对象，包含changes和statistics属性
   */
  async diffSnapshots(id, snapshotId1, snapshotId2) {
    const response = await api.get(
      `/Repository/${id}/snapshots/${snapshotId1}/diff/${snapshotId2}`
    );
    return response.data;
  },
};