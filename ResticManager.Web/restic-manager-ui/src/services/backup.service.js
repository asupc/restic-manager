import api from "./api";

export const backupService = {
  // 获取所有任务
  async getTasks() {
    const response = await api.get("/backuptasks");
    return response.data;
  },

  // 创建任务
  async createTask(taskData) {
    const response = await api.post("/backuptasks", taskData);
    return response.data;
  },

  // 更新任务
  async updateTask(id, taskData) {
    const response = await api.put(`/backuptasks/${id}`, taskData);
    return response.data;
  },

  // 删除任务
  async deleteTask(id) {
    await api.delete(`/backuptasks/${id}`);
  },

  // 运行任务
  async runTask(id) {
    const response = await api.post(`/backuptasks/${id}/run`);
    return response.data;
  },

  // 初始化仓库
  async initializeRepository(repoPath, password) {
    const response = await api.post("/backuptasks/init-repo", {
      repoPath,
      password,
    });
    return response.data;
  },

  // 获取文件夹内容
  async getFolders(path = "") {
    const response = await api.get(`/backuptasks/folders`, {
      params: { path },
    });
    return response.data;
  },
};
