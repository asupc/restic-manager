<template>
  <div id="app">
    <router-view v-if="isAuthPage" />
    <div v-else class="app-layout">
      <!-- 移动端抽屉遮罩 -->
      <transition name="mask-fade">
        <div
          v-if="mobileSidebarOpen"
          class="app-drawer-mask"
          @click="mobileSidebarOpen = false"
        ></div>
      </transition>

      <app-navbar
        :collapsed="sidebarCollapsed"
        :mobile-open="mobileSidebarOpen"
        :theme="theme"
        @toggle="toggleSidebar"
        @close-mobile="mobileSidebarOpen = false"
        @toggle-theme="toggleTheme"
      />

      <main class="app-main" :class="{ 'app-main--expanded': sidebarCollapsed }">
        <!-- 吸顶毛玻璃命令顶栏 -->
        <header class="app-topbar">
          <div class="app-topbar__left">
            <button
              type="button"
              class="topbar-icon-btn topbar-menu-btn"
              aria-label="打开导航菜单"
              @click="mobileSidebarOpen = true"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <line x1="3" y1="6" x2="21" y2="6" />
                <line x1="3" y1="12" x2="21" y2="12" />
                <line x1="3" y1="18" x2="21" y2="18" />
              </svg>
            </button>

            <nav class="topbar-breadcrumb" aria-label="面包屑导航">
              <span
                v-for="(crumb, idx) in breadcrumbs"
                :key="crumb.label + '-' + idx"
                class="topbar-breadcrumb__item"
              >
                <span v-if="idx > 0" class="topbar-breadcrumb__sep">/</span>
                <router-link
                  v-if="crumb.to && idx < breadcrumbs.length - 1"
                  :to="crumb.to"
                  class="topbar-breadcrumb__link"
                >
                  {{ crumb.label }}
                </router-link>
                <span
                  v-else
                  :class="idx === breadcrumbs.length - 1 ? 'topbar-breadcrumb__current' : 'topbar-breadcrumb__group'"
                >
                  {{ crumb.label }}
                </span>
              </span>
            </nav>
          </div>

          <div class="app-topbar__right">
            <!-- 全局快捷命令入口 -->
            <button
              type="button"
              class="topbar-cmd-btn"
              title="打开全局命令面板"
              @click="commandPaletteVisible = true"
            >
              <svg class="topbar-cmd-btn__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
              <span class="topbar-cmd-btn__text">快速跳转 / 命令</span>
              <kbd class="rm-kbd topbar-cmd-btn__kbd">{{ shortcutLabel }}</kbd>
            </button>

            <!-- 后台任务实时状态胶囊 -->
            <el-popover
              placement="bottom-end"
              width="352"
              trigger="click"
              popper-class="rm-task-popover"
            >
              <div class="task-panel">
                <div class="task-panel__section">
                  <div class="task-panel__header">
                    <span>进行中</span>
                    <span class="task-panel__count">{{ activeTasks.length }}</span>
                  </div>
                  <div v-if="activeTasks.length === 0" class="task-panel__empty">
                    当前没有运行中的后台任务
                  </div>
                  <div v-else class="task-panel__list">
                    <div
                      v-for="task in activeTasks"
                      :key="'active-' + task.id"
                      class="task-panel__item"
                    >
                      <span class="task-panel__icon task-panel__icon--running">
                        <i :class="task.status === 'Pending' ? 'el-icon-time' : 'el-icon-loading'"></i>
                      </span>
                      <div class="task-panel__body">
                        <div class="task-panel__title">{{ task.title || '后台任务' }}</div>
                        <div class="task-panel__meta">
                          <span>{{ task.status === 'Pending' ? '排队中' : '执行中' }}</span>
                          <span v-if="formatTaskDuration(task)"> · {{ formatTaskDuration(task) }}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                <div class="task-panel__divider"></div>

                <div class="task-panel__section">
                  <div class="task-panel__header">
                    <span>最近完成</span>
                    <span class="task-panel__count">{{ recentlyCompleted.length }}</span>
                  </div>
                  <div v-if="recentlyCompleted.length === 0" class="task-panel__empty">
                    暂无近期完成的任务
                  </div>
                  <div v-else class="task-panel__list">
                    <div
                      v-for="task in recentlyCompleted"
                      :key="'done-' + task.id"
                      class="task-panel__item"
                    >
                      <span
                        class="task-panel__icon"
                        :class="task.status === 'Failed' ? 'task-panel__icon--danger' : 'task-panel__icon--success'"
                      >
                        <i :class="task.status === 'Failed' ? 'el-icon-circle-close' : 'el-icon-circle-check'"></i>
                      </span>
                      <div class="task-panel__body">
                        <div class="task-panel__title">{{ task.title || '后台任务' }}</div>
                        <div class="task-panel__meta">
                          <span>{{ taskStatusText(task.status) }}</span>
                          <span v-if="formatTaskDuration(task)"> · {{ formatTaskDuration(task) }}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <button
                slot="reference"
                type="button"
                class="topbar-task-btn"
                :class="{ 'topbar-task-btn--active': activeCount > 0 }"
                :title="activeCount > 0 ? activeCount + ' 个任务执行中' : '后台任务'"
              >
                <template v-if="activeCount > 0">
                  <span class="rm-status-pill rm-status-pill--running topbar-task-pill">
                    <span class="rm-status-pill__dot"></span>
                    <span>{{ activeCount }} 个任务执行中</span>
                  </span>
                </template>
                <template v-else>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
                  </svg>
                  <span v-if="recentlyCompleted.length > 0" class="topbar-task-dot"></span>
                </template>
              </button>
            </el-popover>

            <!-- 深 / 浅主题切换按钮 -->
            <button
              type="button"
              class="topbar-icon-btn"
              :title="theme === 'dark' ? '切换到浅色模式' : '切换到深色模式'"
              :aria-label="theme === 'dark' ? '切换到浅色模式' : '切换到深色模式'"
              @click="toggleTheme"
            >
              <svg v-if="theme === 'dark'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="5" />
                <line x1="12" y1="1" x2="12" y2="3" />
                <line x1="12" y1="21" x2="12" y2="23" />
                <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
                <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
                <line x1="1" y1="12" x2="3" y2="12" />
                <line x1="21" y1="12" x2="23" y2="12" />
                <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
                <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
              </svg>
              <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
              </svg>
            </button>
          </div>
        </header>

        <div
          class="app-content"
          v-loading.fullscreen.lock="loading"
          :element-loading-text="loadingText"
        >
          <transition name="page-fade" mode="out-in">
            <router-view />
          </transition>
        </div>
      </main>

      <!-- 全局 Ctrl+K 命令面板 -->
      <command-palette
        :visible.sync="commandPaletteVisible"
        :theme="theme"
        @toggle-theme="toggleTheme"
      />
    </div>
  </div>
</template>

<script>
import AppNavbar from '@/components/AppNavbar.vue';
import CommandPalette from '@/components/CommandPalette.vue';

const THEME_STORAGE_KEY = 'rm_theme';
const SIDEBAR_STORAGE_KEY = 'rm_sidebar_collapsed';

export default {
  name: 'App',
  components: { AppNavbar, CommandPalette },
  data() {
    return {
      sidebarCollapsed: false,
      mobileSidebarOpen: false,
      commandPaletteVisible: false,
      theme: 'dark',
      isMac: false
    };
  },
  computed: {
    isAuthPage() {
      const path = this.$route ? this.$route.path : '';
      return path === '/login' || path === '/register';
    },
    loading() {
      return this.$store.getters.loading;
    },
    loadingText() {
      return this.$store.getters.loadingText;
    },
    activeTasks() {
      return this.$store.getters['tasks/activeTasks'] || [];
    },
    activeCount() {
      return this.$store.getters['tasks/activeCount'] || 0;
    },
    recentlyCompleted() {
      const getterVal = this.$store.getters['tasks/recentlyCompleted'];
      if (Array.isArray(getterVal)) return getterVal;
      const tasksState = this.$store.state && this.$store.state.tasks;
      return (tasksState && tasksState.recentlyCompleted) || [];
    },
    shortcutLabel() {
      return this.isMac ? '⌘K' : 'Ctrl K';
    },
    breadcrumbs() {
      const route = this.$route || {};
      const name = route.name || '';
      const path = route.path || '/';
      const params = route.params || {};

      if (name === 'Home' || path === '/') {
        return [{ label: '工作台' }];
      }
      if (name === 'BackupTasks' || path === '/tasks') {
        return [{ label: '备份中心' }, { label: '备份任务' }];
      }
      if (name === 'BackupRecords' || path === '/records') {
        return [{ label: '备份中心' }, { label: '备份记录' }];
      }
      if (name === 'BackupRules' || path === '/rules') {
        return [{ label: '备份中心' }, { label: '备份规则' }];
      }
      if (name === 'Repositories' || path === '/repositories') {
        return [{ label: '存储与恢复' }, { label: '备份仓库' }];
      }
      if (name === 'RepositorySnapshots') {
        return [
          { label: '备份仓库', to: '/repositories' },
          { label: '仓库快照' }
        ];
      }
      if (name === 'SnapshotFiles') {
        const repoId = params.repositoryId || params.id;
        const crumbs = [{ label: '备份仓库', to: '/repositories' }];
        if (repoId) {
          crumbs.push({ label: '仓库快照', to: '/repositories/' + repoId + '/snapshots' });
        } else {
          crumbs.push({ label: '仓库快照' });
        }
        crumbs.push({ label: '快照文件' });
        return crumbs;
      }
      if (name === 'BackupManagement' || path === '/backup-management') {
        return [{ label: '存储与恢复' }, { label: '快照浏览' }];
      }
      if (name === 'SystemLogs' || path === '/logs') {
        return [{ label: '系统运维' }, { label: '系统日志' }];
      }
      if (name === 'Settings' || path === '/settings') {
        return [{ label: '系统运维' }, { label: '系统设置' }];
      }
      return [{ label: '控制台' }];
    }
  },
  mounted() {
    this.isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad|iPod/i.test(navigator.platform || '');
    this.initPreferences();
    this.syncTaskPolling();
    window.addEventListener('keydown', this.handleGlobalKeydown);
    this.$root.$on('theme-changed', this.handleExternalThemeChange);
  },
  beforeDestroy() {
    window.removeEventListener('keydown', this.handleGlobalKeydown);
    this.$root.$off('theme-changed', this.handleExternalThemeChange);
    this.$store.dispatch('tasks/stop');
  },
  watch: {
    '$route'(to) {
      this.mobileSidebarOpen = false;
      this.commandPaletteVisible = false;
      this.syncTaskPolling(to);
    }
  },
  methods: {
    initPreferences() {
      const savedTheme = localStorage.getItem(THEME_STORAGE_KEY);
      const nextTheme = savedTheme === 'light' || savedTheme === 'dark' ? savedTheme : 'dark';
      this.applyTheme(nextTheme, false);

      const savedCollapsed = localStorage.getItem(SIDEBAR_STORAGE_KEY);
      this.sidebarCollapsed = savedCollapsed === 'true';
    },
    applyTheme(nextTheme, shouldEmit) {
      const normalized = nextTheme === 'light' ? 'light' : 'dark';
      this.theme = normalized;
      document.documentElement.setAttribute('data-theme', normalized);
      localStorage.setItem(THEME_STORAGE_KEY, normalized);
      if (shouldEmit) {
        this.$root.$emit('theme-changed', normalized);
      }
    },
    toggleTheme() {
      const next = this.theme === 'dark' ? 'light' : 'dark';
      this.applyTheme(next, true);
    },
    handleExternalThemeChange(nextTheme) {
      if ((nextTheme === 'light' || nextTheme === 'dark') && nextTheme !== this.theme) {
        this.applyTheme(nextTheme, false);
      }
    },
    toggleSidebar() {
      this.sidebarCollapsed = !this.sidebarCollapsed;
      localStorage.setItem(SIDEBAR_STORAGE_KEY, String(this.sidebarCollapsed));
    },
    handleGlobalKeydown(e) {
      if (this.isAuthPage) return;
      const key = e && e.key ? e.key.toLowerCase() : '';
      if ((e.ctrlKey || e.metaKey) && key === 'k') {
        e.preventDefault();
        this.commandPaletteVisible = !this.commandPaletteVisible;
      }
    },
    taskStatusText(status) {
      if (status === 'Succeeded') return '已完成';
      if (status === 'Failed') return '执行失败';
      if (status === 'Pending') return '排队中';
      if (status === 'Running') return '执行中';
      return status || '已完成';
    },
    formatTaskDuration(task) {
      if (!task) return '';
      const startRaw = task.startedAt || task.startTime || task.createdAt;
      if (!startRaw) return '';
      const startMs = new Date(startRaw).getTime();
      if (isNaN(startMs)) return '';
      const endRaw = task.endedAt || task.endTime || task.completedAt;
      const endMs = endRaw ? new Date(endRaw).getTime() : Date.now();
      if (isNaN(endMs)) return '';
      const diffSec = Math.max(0, Math.floor((endMs - startMs) / 1000));
      if (diffSec < 60) return diffSec + ' 秒';
      const mins = Math.floor(diffSec / 60);
      const secs = diffSec % 60;
      if (mins < 60) return mins + ' 分 ' + secs + ' 秒';
      const hours = Math.floor(mins / 60);
      return hours + ' 小时 ' + (mins % 60) + ' 分';
    },
    // 登录后启动后台任务全局轮询（顶栏/侧边栏状态 + 完成通知），离开业务页/登出时停止
    syncTaskPolling(to) {
      const path = to ? to.path : this.$route.path;
      const isAuthPage = path === '/login' || path === '/register';
      if (!isAuthPage && localStorage.getItem('token')) {
        this.$store.dispatch('tasks/start');
      } else {
        this.$store.dispatch('tasks/stop');
      }
    }
  }
};
</script>

<style>
/* 吸顶毛玻璃命令顶栏 */
.app-topbar {
  position: sticky;
  top: 0;
  z-index: 80;
  height: var(--topbar-height);
  padding: 0 var(--space-8);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  background: var(--color-bg-topbar);
  border-bottom: 1px solid var(--color-border);
  backdrop-filter: blur(14px);
  -webkit-backdrop-filter: blur(14px);
}

.app-topbar__left,
.app-topbar__right {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
}

.topbar-menu-btn {
  display: none !important;
}

.topbar-breadcrumb {
  display: flex;
  align-items: center;
  min-width: 0;
  font-size: var(--font-sm);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.topbar-breadcrumb__item {
  display: inline-flex;
  align-items: center;
  min-width: 0;
}

.topbar-breadcrumb__sep {
  margin: 0 8px;
  color: var(--color-text-placeholder);
  font-size: 12px;
}

.topbar-breadcrumb__group {
  color: var(--color-text-secondary);
  font-weight: 500;
}

.topbar-breadcrumb__link {
  color: var(--color-text-secondary);
  text-decoration: none;
  font-weight: 500;
  transition: color var(--transition-fast);
}

.topbar-breadcrumb__link:hover {
  color: var(--brand-400);
}

.topbar-breadcrumb__current {
  color: var(--color-text-primary);
  font-weight: 600;
}

/* 全局命令按钮 */
.topbar-cmd-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  height: 34px;
  padding: 0 10px 0 12px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  font-size: var(--font-xs);
  cursor: pointer;
  transition: border-color var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast);
}

.topbar-cmd-btn:hover {
  background: var(--color-bg-hover);
  border-color: var(--brand-200);
  color: var(--color-text-primary);
}

.topbar-cmd-btn__icon {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.topbar-cmd-btn__text {
  white-space: nowrap;
}

/* 顶栏图标按钮 */
.topbar-icon-btn {
  width: 34px;
  height: 34px;
  padding: 0;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  flex-shrink: 0;
  transition: border-color var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast);
}

.topbar-icon-btn:hover {
  background: var(--color-bg-hover);
  border-color: var(--brand-200);
  color: var(--brand-400);
}

.topbar-icon-btn svg {
  width: 16px;
  height: 16px;
}

/* 后台任务触发按钮 */
.topbar-task-btn {
  position: relative;
  height: 34px;
  min-width: 34px;
  padding: 0 9px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: border-color var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast);
}

.topbar-task-btn:hover {
  background: var(--color-bg-hover);
  border-color: var(--brand-200);
  color: var(--brand-400);
}

.topbar-task-btn--active {
  padding: 0;
  border: none;
  background: transparent;
}

.topbar-task-btn--active:hover {
  background: transparent;
}

.topbar-task-pill {
  height: 32px;
  padding: 0 12px;
}

.topbar-task-btn svg {
  width: 16px;
  height: 16px;
}

.topbar-task-dot {
  position: absolute;
  top: 6px;
  right: 6px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--success-500);
}

/* 后台任务 Popover 内容 */
.rm-task-popover {
  padding: 12px 14px !important;
}

.task-panel__section {
  display: flex;
  flex-direction: column;
}

.task-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin-bottom: 8px;
}

.task-panel__count {
  min-width: 20px;
  height: 18px;
  padding: 0 6px;
  border-radius: var(--radius-full);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
  font-size: 11px;
  font-variant-numeric: tabular-nums;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.task-panel__empty {
  padding: 12px 0;
  text-align: center;
  font-size: 12px;
  color: var(--color-text-placeholder);
}

.task-panel__list {
  max-height: 180px;
  overflow-y: auto;
}

.task-panel__item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 8px 6px;
  border-radius: var(--radius-sm);
}

.task-panel__item + .task-panel__item {
  border-top: 1px solid var(--color-border-soft);
}

.task-panel__icon {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  flex-shrink: 0;
  margin-top: 1px;
}

.task-panel__icon--running {
  background: var(--brand-50);
  color: var(--brand-400);
}

.task-panel__icon--success {
  background: var(--success-50);
  color: var(--success-600);
}

.task-panel__icon--danger {
  background: var(--danger-50);
  color: var(--danger-600);
}

.task-panel__body {
  min-width: 0;
  flex: 1;
}

.task-panel__title {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
  word-break: break-word;
  line-height: 1.4;
}

.task-panel__meta {
  font-size: 11px;
  color: var(--color-text-secondary);
  margin-top: 2px;
  font-variant-numeric: tabular-nums;
}

.task-panel__divider {
  height: 1px;
  background: var(--color-border);
  margin: 12px 0;
}

/* 移动端抽屉遮罩 */
.app-drawer-mask {
  display: none;
}

.mask-fade-enter-active,
.mask-fade-leave-active {
  transition: opacity 200ms var(--ease-out);
}
.mask-fade-enter,
.mask-fade-leave-to {
  opacity: 0;
}

@media (max-width: 960px) {
  .app-topbar {
    padding: 0 var(--space-4);
  }
  .topbar-menu-btn {
    display: inline-flex !important;
  }
  .topbar-cmd-btn__text,
  .topbar-cmd-btn__kbd {
    display: none;
  }
  .topbar-cmd-btn {
    width: 34px;
    padding: 0;
    justify-content: center;
  }
  .app-drawer-mask {
    display: block;
    position: fixed;
    inset: 0;
    z-index: 120;
    background: var(--color-bg-backdrop);
    backdrop-filter: blur(4px);
    -webkit-backdrop-filter: blur(4px);
  }
}
</style>