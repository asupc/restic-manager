<template>
  <aside class="rm-sidebar" :class="{ 'rm-sidebar--collapsed': collapsed }">
    <!-- 品牌区 -->
    <div class="rm-sidebar__brand">
      <div class="rm-logo">
        <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
          <rect x="2" y="6" width="28" height="22" rx="5" fill="#06b6d4"/>
          <path d="M9 16l4 4 10-10" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
      <div v-if="!collapsed" class="rm-brand-text">
        <div class="rm-brand-title">ResticManager</div>
        <div class="rm-brand-sub">智能备份管家</div>
      </div>
    </div>

    <!-- 折叠按钮 -->
    <button class="rm-sidebar__toggle" @click="$emit('toggle')" :title="collapsed ? '展开' : '折叠'">
      <i :class="collapsed ? 'el-icon-arrow-right' : 'el-icon-arrow-left'"></i>
    </button>

    <!-- 菜单 -->
    <nav class="rm-menu">
      <div v-if="!collapsed" class="rm-menu__group-title">概览</div>
      <router-link
        v-for="item in mainMenu"
        :key="item.path"
        :to="item.path"
        class="rm-menu__item"
        :class="{ 'rm-menu__item--active': isActive(item.path) }"
        :title="collapsed ? item.label : ''"
      >
        <span class="rm-menu__icon" v-html="item.icon"></span>
        <span v-if="!collapsed" class="rm-menu__label">{{ item.label }}</span>
        <span v-if="!collapsed && item.badge" class="rm-menu__badge">{{ item.badge }}</span>
      </router-link>

      <div v-if="!collapsed" class="rm-menu__group-title" style="margin-top: 24px;">快捷</div>
      <div class="rm-menu__item rm-menu__item--static" @click="quickAction('addTask')" title="新增任务">
        <span class="rm-menu__icon"><i class="el-icon-plus"></i></span>
        <span v-if="!collapsed" class="rm-menu__label">新增任务</span>
      </div>
      <div class="rm-menu__item rm-menu__item--static" @click="quickAction('addRepo')" title="新建仓库">
        <span class="rm-menu__icon"><i class="el-icon-folder-add"></i></span>
        <span v-if="!collapsed" class="rm-menu__label">新建仓库</span>
      </div>

      <!-- 后台任务：角标显示进行中数量，点击弹出任务列表 -->
      <el-popover placement="right-start" width="320" trigger="click">
        <div class="task-pop">
          <div v-if="activeTasks.length === 0" class="task-pop__empty">当前没有进行中的后台任务</div>
          <div v-else class="task-pop__list">
            <div v-for="task in activeTasks" :key="task.id" class="task-pop__item">
              <i :class="task.status === 'Pending' ? 'el-icon-time' : 'el-icon-loading'" class="task-pop__icon"></i>
              <div class="task-pop__main">
                <div class="task-pop__title">{{ task.title }}</div>
                <div class="task-pop__meta">{{ task.status === 'Pending' ? '排队中' : '执行中' }}</div>
              </div>
            </div>
          </div>
        </div>
        <div slot="reference" class="rm-menu__item rm-menu__item--static" title="后台任务">
          <span class="rm-menu__icon"><i class="el-icon-data-line"></i></span>
          <span v-if="!collapsed" class="rm-menu__label">后台任务</span>
          <span v-if="!collapsed && activeCount" class="rm-menu__badge">{{ activeCount }}</span>
          <span v-else-if="collapsed && activeCount" class="task-dot"></span>
        </div>
      </el-popover>
    </nav>

    <!-- 底部用户区 -->
    <div class="rm-sidebar__footer">
      <div class="rm-user-card" :title="userName">
        <div class="rm-avatar">{{ avatarLetter }}</div>
        <div v-if="!collapsed" class="rm-user-info">
          <div class="rm-user-name">{{ userName }}</div>
          <div class="rm-user-role">管理员</div>
        </div>
      </div>
      <el-dropdown v-if="!collapsed" trigger="click" @command="handleUserCmd" class="rm-user-menu">
        <span class="rm-user-trigger">
          <i class="el-icon-more"></i>
        </span>
        <el-dropdown-menu slot="dropdown">
          <el-dropdown-item icon="el-icon-refresh-right" command="refresh">刷新数据</el-dropdown-item>
          <el-dropdown-item icon="el-icon-question" command="about">关于</el-dropdown-item>
          <el-dropdown-item divided icon="el-icon-switch-button" command="logout">登出</el-dropdown-item>
        </el-dropdown-menu>
      </el-dropdown>
      <div v-else class="rm-user-trigger rm-user-trigger--icon" @click="confirmLogout" title="登出">
        <i class="el-icon-switch-button"></i>
      </div>
    </div>
  </aside>
</template>

<script>
import { authService } from '@/services/auth.service';

export default {
  name: 'AppNavbar',
  props: {
    collapsed: { type: Boolean, default: false }
  },
  data() {
    return {
      userName: '用户',
      avatarLetter: 'U',
      mainMenu: [
        {
          path: '/',
          label: '工作台',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="9"/><rect x="14" y="3" width="7" height="5"/><rect x="14" y="12" width="7" height="9"/><rect x="3" y="16" width="7" height="5"/></svg>'
        },
        {
          path: '/tasks',
          label: '备份任务',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>'
        },
        {
          path: '/records',
          label: '备份记录',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><line x1="10" y1="9" x2="8" y2="9"/></svg>'
        },
        {
          path: '/rules',
          label: '备份规则',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/><path d="M9 13h6M9 17h6"/></svg>'
        },
        {
          path: '/repositories',
          label: '备份仓库',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><path d="M3.27 6.96 12 12.01l8.73-5.05M12 22.08V12"/></svg>'
        },
        {
          path: '/backup-management',
          label: '快照浏览',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>'
        },
        {
          path: '/settings',
          label: '系统设置',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>'
        }
      ]
    };
  },
  mounted() {
    this.loadUser();
  },
  computed: {
    activeTasks() {
      return this.$store.getters['tasks/activeTasks'];
    },
    activeCount() {
      return this.$store.getters['tasks/activeCount'];
    }
  },
  watch: {
    '$route'() {
      this.loadUser();
    }
  },
  methods: {
    loadUser() {
      try {
        const raw = localStorage.getItem('user');
        if (raw) {
          const user = JSON.parse(raw);
          this.userName = user.username || user.name || '用户';
          this.avatarLetter = (this.userName || 'U').charAt(0).toUpperCase();
        }
      } catch (e) {
        // 清理历史遗留的非法值(如字符串 "undefined"),避免控制台反复报错
        localStorage.removeItem('user');
      }
    },
    isActive(path) {
      if (path === '/') return this.$route.path === '/';
      return this.$route.path.startsWith(path);
    },
    quickAction(type) {
      if (type === 'addTask') this.$router.push('/tasks?action=new');
      else if (type === 'addRepo') this.$router.push('/repositories?action=new');
    },
    handleUserCmd(cmd) {
      if (cmd === 'logout') return this.confirmLogout();
      if (cmd === 'refresh') {
        this.$message.success('已刷新');
        this.$router.go(0);
      }
      if (cmd === 'about') {
        this.$alert('ResticManager 是一个基于 restic 的备份图形化管理工具。\n版本 1.0.0', '关于', {
          confirmButtonText: '好的'
        });
      }
    },
    confirmLogout() {
      this.$confirm('确定要登出吗？', '确认登出', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }).then(() => {
        // 停止后台任务全局轮询
        this.$store.dispatch('tasks/stop');
        authService.logout();
        this.$router.push('/login');
      }).catch(() => {});
    }
  }
};
</script>

<style scoped>
.rm-sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: var(--sidebar-width);
  background: var(--color-bg-sidebar);
  color: var(--color-text-on-dark);
  display: flex;
  flex-direction: column;
  z-index: 100;
  transition: width var(--transition-base);
  border-right: 1px solid rgba(255, 255, 255, 0.06);
}

.rm-sidebar--collapsed {
  width: var(--sidebar-width-collapsed);
}

.rm-sidebar__brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: 18px 20px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.rm-logo {
  width: 36px;
  height: 36px;
  flex-shrink: 0;
}
.rm-logo svg { width: 100%; height: 100%; }

.rm-brand-text { line-height: 1.2; min-width: 0; }
.rm-brand-title {
  font-size: var(--font-lg);
  font-weight: 700;
  color: #fff;
  white-space: nowrap;
  letter-spacing: 0.01em;
}
.rm-brand-sub {
  font-size: 11px;
  color: var(--gray-400);
  margin-top: 2px;
  white-space: nowrap;
}

.rm-sidebar__toggle {
  position: absolute;
  top: 24px;
  right: -12px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: none;
  background: var(--brand-500);
  color: #fff;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  box-shadow: none;
  z-index: 10;
  transition: transform var(--transition-fast), background-color var(--transition-fast);
}
.rm-sidebar__toggle:hover {
  transform: scale(1.12);
  background: var(--brand-600);
}

.rm-menu {
  flex: 1;
  padding: 16px 12px;
  overflow-y: auto;
}

.rm-menu__group-title {
  font-size: 11px;
  text-transform: uppercase;
  color: #7d8db0;
  letter-spacing: 0.08em;
  padding: 0 12px 8px;
  font-weight: 600;
}

.rm-menu__item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  color: #cbd5e1;
  text-decoration: none;
  font-size: var(--font-base);
  margin-bottom: 2px;
  position: relative;
  cursor: pointer;
  transition: all var(--transition-fast);
  user-select: none;
}

.rm-menu__item:hover {
  background: var(--color-bg-sidebar-hov);
  color: #fff;
  transform: translateX(3px);
}

.rm-menu__item--active {
  background: var(--brand-600);
  color: #fff;
}
.rm-menu__item--active:hover {
  background: var(--brand-600);
  transform: none;
}

.rm-menu__item--active::before {
  content: '';
  position: absolute;
  left: -12px;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 18px;
  background: var(--brand-400);
  border-radius: 0 2px 2px 0;
}

.rm-menu__icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}
.rm-menu__icon svg { width: 18px; height: 18px; }
.rm-menu__icon i { font-size: 18px; }

.rm-menu__label {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.rm-menu__badge {
  background: var(--brand-400);
  color: #fff;
  font-size: 11px;
  font-weight: 600;
  padding: 1px 7px;
  border-radius: var(--radius-full);
}

.rm-menu__item--static {
  border: 1px dashed rgba(255, 255, 255, 0.08);
  background: transparent;
}
.rm-menu__item--static:hover {
  border-color: rgba(255, 255, 255, 0.16);
}

/* 折叠态下有活动任务时的小圆点 */
.task-dot {
  position: absolute;
  top: 8px;
  right: 8px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--brand-400);
}

/* 后台任务 popover 列表 */
.task-pop__empty {
  padding: 18px 0;
  text-align: center;
  font-size: 12px;
  color: var(--color-text-secondary);
}
.task-pop__list {
  max-height: 320px;
  overflow-y: auto;
}
.task-pop__item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 8px 4px;
}
.task-pop__item + .task-pop__item {
  border-top: 1px dashed var(--color-border);
}
.task-pop__icon {
  font-size: 16px;
  color: var(--brand-500);
  margin-top: 2px;
}
.task-pop__main {
  min-width: 0;
}
.task-pop__title {
  font-size: 13px;
  color: var(--color-text-primary);
  word-break: break-all;
}
.task-pop__meta {
  font-size: 11px;
  color: var(--color-text-secondary);
  margin-top: 2px;
}

.rm-sidebar__footer {
  border-top: 1px solid rgba(255, 255, 255, 0.06);
  padding: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.rm-user-card {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px;
  border-radius: var(--radius-md);
  min-width: 0;
  cursor: default;
}
.rm-user-card:hover { background: var(--color-bg-sidebar-hov); }

.rm-avatar {
  width: 34px;
  height: 34px;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--brand-500);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-weight: 700;
  font-size: 15px;
}

.rm-user-info {
  min-width: 0;
  flex: 1;
}
.rm-user-name {
  font-size: 13px;
  font-weight: 600;
  color: #fff;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rm-user-role {
  font-size: 11px;
  color: var(--gray-400);
}

.rm-user-trigger {
  width: 30px;
  height: 30px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--gray-400);
  cursor: pointer;
  transition: all var(--transition-fast);
}
.rm-user-trigger:hover {
  background: var(--color-bg-sidebar-hov);
  color: #fff;
}
.rm-user-trigger--icon {
  margin: 0 auto;
}
</style>