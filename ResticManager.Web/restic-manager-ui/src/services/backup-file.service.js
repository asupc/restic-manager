import api from "./api";

export const backupFileService = {
  /**
   * 根据任务ID获取备份文件
   */
  async getFilesByTaskId(taskId) {
    const response = await api.get(`/backupfiles/${taskId}/files`);
    return response.data;
  },

  /**
   * 下载备份文件
   */
  async downloadFile(taskId, fileId) {
    const response = await api.get(
      `/backupfiles/${taskId}/download/${fileId}`,
      {
        responseType: "blob",
      }
    );
    return response.data;
  },
};
