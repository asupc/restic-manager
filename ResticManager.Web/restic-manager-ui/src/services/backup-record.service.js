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
   * 分页获取备份记录（支持任务ID、状态、关键词筛选）
   */
  async getRecordsPaged({ page = 1, pageSize = 10, taskId = null, status = null, keyword = null } = {}) {
    const params = { page, pageSize };
    if (taskId) params.taskId = taskId;
    if (status) params.status = status;
    if (keyword) params.keyword = keyword;
    const response = await api.get("/backuprecords/paged", { params });
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
   * 分页根据任务ID获取备份记录
   */
  async getRecordsByTaskIdPaged(taskId, { page = 1, pageSize = 10 } = {}) {
    const response = await api.get(`/backuprecords/task/${taskId}/paged`, {
      params: { page, pageSize }
    });
    return response.data;
  },

  /**
   * 获取指定备份记录详情
   */
  async getRecordById(id) {
    const response = await api.get(`/backuprecords/${id}`);
    return response.data;
  },

  /**
   * 删除备份记录
   */
  async deleteRecord(id) {
    await api.delete(`/backuprecords/${id}`);
  },
};
