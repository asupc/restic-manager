import api from "./api";

// 与后端 AsyncTaskTypes 一一对应的任务类型常量
export const TASK_TYPES = {
  RepositoryInitialize: "RepositoryInitialize",
  RepositoryCheck: "RepositoryCheck",
  RepositoryStats: "RepositoryStats",
  SnapshotSync: "SnapshotSync",
  SnapshotFileTree: "SnapshotFileTree",
  SnapshotDiff: "SnapshotDiff",
  SnapshotRestore: "SnapshotRestore",
  FileRestore: "FileRestore",
  SnapshotDelete: "SnapshotDelete",
  SnapshotBatchDelete: "SnapshotBatchDelete",
  RepositoryUnlock: "RepositoryUnlock",
  RepositoryPrune: "RepositoryPrune",
  RepositoryRepairIndex: "RepositoryRepairIndex",
  RepositoryRepairSnapshots: "RepositoryRepairSnapshots",
  RepositoryDeepCheck: "RepositoryDeepCheck",
  SnapshotFind: "SnapshotFind",
  SnapshotTagSet: "SnapshotTagSet",
  BackupRun: "BackupRun"
};

// 后台异步任务查询服务。
// 所有仓库类慢操作提交后立即返回 { taskId }，通过这里轮询状态与结果。
// 全部请求带 loading: false，不触发全局全屏 loading。
export const taskService = {
  // 获取单个任务（status / resultJson / error）
  async getTask(id) {
    const response = await api.get(`/asynctasks/${id}`, { loading: false });
    return response.data;
  },

  // 全局轮询聚合接口：一次返回进行中任务 + 最近完成任务
  async getDashboard() {
    const response = await api.get("/asynctasks/dashboard", { loading: false });
    return response.data;
  },

  // 获取全部进行中（Pending/Running）任务
  async getActiveTasks() {
    const response = await api.get("/asynctasks/active", { loading: false });
    return response.data;
  }
};
