import api from "./api";

export const backupRecordService = {
  /**
   * 获取所有备份记录
   */
  async getRecords() {
    const response = await api.get("/backuprecords");
    return response.data;
  },

  /**
   * 根据任务ID获取备份记录
   */
  async getRecordsByTaskId(taskId) {
    const response = await api.get(`/backuprecords/task/${taskId}`);
    return response.data;
  },

  /**
   * 删除备份记录
   */
  async deleteRecord(id) {
    await api.delete(`/backuprecords/${id}`);
  },
};
