import { Notification } from "element-ui";
import { taskService } from "../../services/task.service";

// 双频率轮询：有活动任务时 2s（按钮态/角标快速刷新），空闲时 30s
// （空闲轮询让 Quartz 定时备份的开始/完成通知也能跨页面弹出，代价可忽略）
const ACTIVE_INTERVAL = 2000;
const IDLE_INTERVAL = 30000;

// 定时器不放响应式 state（无需驱动视图），模块级变量即可
let timer = null;

const state = {
  // 进行中（Pending/Running）的任务列表
  activeTasks: [],
  // 最近完成的任务（后端返回 EndTime 在 2 分钟内的记录）
  recentlyCompleted: [],
  // 已弹过通知的任务 id → true，保证完成通知只弹一次
  notifiedIds: {}
};

const getters = {
  activeTasks: state => state.activeTasks,
  activeCount: state => state.activeTasks.length,
  // 某仓库进行中的任务列表（页面 mounted 恢复轮询用）
  activeByRepository: state => repositoryId =>
    state.activeTasks.filter(t => t.repositoryId === Number(repositoryId)),
  // 进行中的 BackupRun 任务对应的备份任务 ID 集合（备份任务列表行运行态派生用）
  activeBackupRunTargetKeys: state =>
    state.activeTasks.filter(t => t.type === "BackupRun").map(t => t.targetKey)
};

const mutations = {
  SET_DASHBOARD(state, { active, recentlyCompleted }) {
    state.activeTasks = active || [];
    state.recentlyCompleted = recentlyCompleted || [];
  },
  MARK_NOTIFIED(state, id) {
    state.notifiedIds = { ...state.notifiedIds, [id]: true };
  },
  CLEAR_NOTIFIED(state) {
    state.notifiedIds = {};
  }
};

const actions = {
  // 登录后（App.vue 挂载/路由切换到业务页时）调用；幂等
  start({ dispatch }) {
    dispatch("tick");
  },

  // 登出时停止轮询
  stop({ commit }) {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
    commit("CLEAR_NOTIFIED");
  },

  // 页面提交任务后调用：立即触发一次 tick，让角标即时刷新
  noteSubmitted({ dispatch }) {
    dispatch("tick");
  },

  tick({ state, commit, dispatch }) {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
    // 未登录不轮询
    if (!localStorage.getItem("token")) return;

    taskService
      .getDashboard()
      .then(dashboard => {
        commit("SET_DASHBOARD", dashboard);

        // 对新完成的任务弹全局通知（每个任务只弹一次）
        (dashboard.recentlyCompleted || []).forEach(task => {
          if (state.notifiedIds[task.id]) return;
          commit("MARK_NOTIFIED", task.id);
          if (task.status === "Succeeded") {
            Notification.success({
              title: "后台任务完成",
              message: task.title,
              duration: 4500
            });
          } else if (task.status === "Failed") {
            Notification.error({
              title: "后台任务失败",
              message: task.error || task.title,
              duration: 0
            });
          }
        });
      })
      .catch(() => {})
      .finally(() => {
        const interval =
          state.activeTasks.length > 0 ? ACTIVE_INTERVAL : IDLE_INTERVAL;
        timer = setTimeout(() => dispatch("tick"), interval);
      });
  }
};

export default {
  namespaced: true,
  state,
  getters,
  mutations,
  actions
};
