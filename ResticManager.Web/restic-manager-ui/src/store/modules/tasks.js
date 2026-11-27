import { Notification } from "element-ui";
import { taskService } from "../../services/task.service";

// 双频率轮询：有活动任务时 2s（按钮态/角标快速刷新），空闲时 30s
// （空闲轮询让 Quartz 定时备份的开始/完成通知也能跨页面弹出，代价可忽略）
const ACTIVE_INTERVAL = 2000;
const IDLE_INTERVAL = 30000;

// 定时器与并发控制状态不放响应式 state（无需驱动视图），模块级变量即可
let timer = null;
let isRunning = false;
let pollGeneration = 0;
let inFlight = false;
let pendingImmediate = false;

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
  SET_DASHBOARD(state, payload) {
    const data = payload || {};
    const recent = data.recentlyCompleted || [];
    state.activeTasks = data.active || [];
    state.recentlyCompleted = recent;

    // 修剪 notifiedIds，仅保留仍存在于本次 recentlyCompleted 中的 ID，消除长期运行内存增长
    const nextNotified = {};
    recent.forEach(task => {
      if (task && state.notifiedIds[task.id]) {
        nextNotified[task.id] = true;
      }
    });
    state.notifiedIds = nextNotified;
  },
  MARK_NOTIFIED(state, id) {
    state.notifiedIds = { ...state.notifiedIds, [id]: true };
  },
  CLEAR_NOTIFIED(state) {
    state.notifiedIds = {};
  },
  RESET_STATE(state) {
    state.activeTasks = [];
    state.recentlyCompleted = [];
    state.notifiedIds = {};
  }
};

const actions = {
  // 登录后（App.vue 挂载/路由切换到业务页时）调用；幂等
  start({ dispatch }) {
    if (isRunning || !localStorage.getItem("token")) return;
    isRunning = true;
    pollGeneration += 1;
    pendingImmediate = false;
    dispatch("tick");
  },

  // 登出或离开业务页时停止轮询，拦截在途请求并清空状态
  stop({ commit }) {
    isRunning = false;
    pollGeneration += 1;
    inFlight = false;
    pendingImmediate = false;
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
    commit("RESET_STATE");
  },

  // 页面提交任务后调用：立即触发一次 tick，让角标即时刷新
  noteSubmitted({ dispatch }) {
    if (!isRunning) {
      dispatch("start");
      return;
    }
    if (inFlight) {
      pendingImmediate = true;
      return;
    }
    dispatch("tick");
  },

  tick({ state, commit, dispatch }) {
    if (!isRunning) return;
    // 未登录不轮询
    if (!localStorage.getItem("token")) {
      isRunning = false;
      return;
    }
    if (inFlight) {
      pendingImmediate = true;
      return;
    }
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }

    inFlight = true;
    const gen = pollGeneration;

    taskService
      .getDashboard({ loading: false, silent: true })
      .then(dashboard => {
        if (!isRunning || gen !== pollGeneration) return;
        const data = dashboard || {};
        commit("SET_DASHBOARD", data);

        // 对新完成的任务弹全局通知（每个任务只弹一次）
        (data.recentlyCompleted || []).forEach(task => {
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
        if (!isRunning || gen !== pollGeneration) return;
        inFlight = false;
        if (timer) {
          clearTimeout(timer);
          timer = null;
        }
        if (pendingImmediate) {
          pendingImmediate = false;
          dispatch("tick");
          return;
        }
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
