<template>
  <aside
    class="rm-sidebar"
    :class="{
      'rm-sidebar--collapsed': collapsed,
      'rm-sidebar--mobile-open': mobileOpen
    }"
  >
    <!-- 品牌区 -->
    <div class="rm-sidebar__brand">
      <router-link to="/" class="rm-brand-link" @click.native="handleNavClick">
        <div class="rm-logo">
          <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <linearGradient id="rmLogoGrad" x1="2" y1="4" x2="30" y2="28" gradientUnits="userSpaceOnUse">
                <stop offset="0%" stop-color="#22d3ee" />
                <stop offset="100%" stop-color="#0891b2" />
              </linearGradient>
            </defs>
            <rect x="2" y="4" width="28" height="24" rx="7" fill="url(#rmLogoGrad)" />
            <path d="M9.5 16.5l4.2 4.2 8.8-9.4" stroke="#ffffff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </div>
        <div v-if="!collapsed || mobileOpen" class="rm-brand-text">
          <div class="rm-brand-title">ResticManager</div>
          <div class="rm-brand-sub">智能备份控制台</div>
        </div>
      </router-link>

      <!-- 移动端抽屉关闭按钮 -->
      <button
        type="button"
        class="rm-sidebar__mobile-close"
        aria-label="关闭导航菜单"
        @click="$emit('close-mobile')"
      >
        <i class="el-icon-close"></i>
      </button>
    </div>

    <!-- 桌面端折叠按钮 -->
    <button
      type="button"
      class="rm-sidebar__toggle"
      @click="$emit('toggle')"
      :title="collapsed ? '展开侧边栏' : '折叠侧边栏'"
    >
      <i :class="collapsed ? 'el-icon-arrow-right' : 'el-icon-arrow-left'"></i>
    </button>

    <!-- 分组导航菜单 -->
    <nav class="rm-menu">
      <div
        v-for="(group, gIdx) in menuGroups"
        :key="group.title"
        class="rm-menu__group"
        :class="{ 'rm-menu__group--spaced': gIdx > 0 }"
      >
        <div v-if="!collapsed || mobileOpen" class="rm-menu__group-title">{{ group.title }}</div>
        <div v-else-if="gIdx > 0" class="rm-menu__divider"></div>

        <router-link
          v-for="item in group.items"
          :key="item.path"
          :to="item.path"
          class="rm-menu__item"
          :class="{ 'rm-menu__item--active': isActive(item.path) }"
          :title="collapsed && !mobileOpen ? item.label : ''"
          @click.native="handleNavClick"
        >
          <span class="rm-menu__icon" v-html="item.icon"></span>
          <span v-if="!collapsed || mobileOpen" class="rm-menu__label">{{ item.label }}</span>
          <span
            v-if="item.path === '/tasks' && activeCount > 0 && (!collapsed || mobileOpen)"
            class="rm-menu__badge"
          >
            {{ activeCount }}
          </span>
          <span
            v-else-if="item.path === '/tasks' && activeCount > 0 && collapsed && !mobileOpen"
            class="rm-menu__dot"
          ></span>
        </router-link>
      </div>
    </nav>

    <!-- 底部快捷操作区 -->
    <div class="rm-sidebar__quick">
      <button
        type="button"
        class="rm-quick-btn"
        :title="collapsed && !mobileOpen ? '新增任务' : ''"
        @click="quickAction('addTask')"
      >
        <i class="el-icon-plus"></i>
        <span v-if="!collapsed || mobileOpen">新增任务</span>
      </button>
      <button
        type="button"
        class="rm-quick-btn"
        :title="collapsed && !mobileOpen ? '新建仓库' : ''"
        @click="quickAction('addRepo')"
      >
        <i class="el-icon-folder-add"></i>
        <span v-if="!collapsed || mobileOpen">新建仓库</span>
      </button>
    </div>

    <!-- 底部用户信息卡 -->
    <div class="rm-sidebar__footer">
      <div class="rm-user-card" :title="userName">
        <div class="rm-avatar">{{ avatarLetter }}</div>
        <div v-if="!collapsed || mobileOpen" class="rm-user-info">
          <div class="rm-user-name">{{ userName }}</div>
          <div class="rm-user-role">管理员</div>
        </div>
      </div>

      <el-dropdown
        v-if="!collapsed || mobileOpen"
        trigger="click"
        placement="top-end"
        @command="handleUserCmd"
        class="rm-user-menu"
      >
        <span class="rm-user-trigger" title="更多操作">
          <i class="el-icon-more"></i>
        </span>
        <el-dropdown-menu slot="dropdown">
          <el-dropdown-item icon="el-icon-refresh-right" command="refresh">刷新数据</el-dropdown-item>
          <el-dropdown-item
            :icon="theme === 'dark' ? 'el-icon-sunny' : 'el-icon-moon'"
            command="toggleTheme"
          >
            {{ theme === 'dark' ? '切换浅色主题' : '切换深色主题' }}
          </el-dropdown-item>
          <el-dropdown-item icon="el-icon-info" command="about">关于</el-dropdown-item>
          <el-dropdown-item divided icon="el-icon-switch-button" command="logout">登出</el-dropdown-item>
        </el-dropdown-menu>
      </el-dropdown>

      <button
        v-else
        type="button"
        class="rm-user-trigger rm-user-trigger--icon"
        title="登出"
        @click="confirmLogout"
      >
        <i class="el-icon-switch-button"></i>
      </button>
    </div>
  </aside>
</template>

<script>
import { authService } from '@/services/auth.service';

export default {
  name: 'AppNavbar',
  props: {
    collapsed: { type: Boolean, default: false },
    mobileOpen: { type: Boolean, default: false },
    theme: { type: String, default: 'dark' }
  },
  data() {
    return {
      userName: '用户',
      avatarLetter: 'U',
      menuGroups: [
        {
          title: '概览',
          items: [
            {
              path: '/',
              label: '工作台',
              icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="9" rx="1.5"/><rect x="14" y="3" width="7" height="5" rx="1.5"/><rect x="14" y="12" width="7" height="9" rx="1.5"/><rect x="3" y="16" width="7" height="5" rx="1.5"/></svg>'
            }
          ]
        },
        {
          title: '备份中心',
          items: [
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
              icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"/></svg>'
            }
          ]
        },
        {
          title: '存储与恢复',
          items: [
            {
              path: '/repositories',
              label: '备份仓库',
              icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><path d="M3.27 6.96 12 12.01l8.73-5.05M12 22.08V12"/></svg>'
            },
            {
              path: '/backup-management',
              label: '快照浏览',
              icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>'
            }
          ]
        },
        {
          title: '系统运维',
          items: [
            {
              path: '/logs',
              label: '系统日志',
              icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 17 10 11 4 5"/><line x1="12" y1="19" x2="20" y2="19"/></svg>'
            },
            {
              path: '/settings',
              label: '系统设置',
              icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>'
            }
          ]
        }
      ]
    };
  },
  mounted() {
    this.loadUser();
  },
  computed: {
    activeCount() {
      return this.$store.getters['tasks/activeCount'] || 0;
    }
  },
  watch: {
    '$route'(to, from) {
      if (from && (from.path === '/login' || from.path === '/register')) {
        this.loadUser();
      }
    }
  },
  methods: {
    loadUser() {
      const raw = localStorage.getItem('user');
      if (raw === this._lastUserRaw) return;
      this._lastUserRaw = raw;
      if (!raw) {
        this.userName = '用户';
        this.avatarLetter = 'U';
        return;
      }
      try {
        const user = JSON.parse(raw);
        this.userName = (user && (user.username || user.name)) || '用户';
        this.avatarLetter = (this.userName || 'U').charAt(0).toUpperCase();
      } catch (e) {
        localStorage.removeItem('user');
        this._lastUserRaw = null;
      }
    },
    isActive(path) {
      if (path === '/') return this.$route.path === '/';
      return this.$route.path.startsWith(path);
    },
    handleNavClick() {
      if (this.mobileOpen) {
        this.$emit('close-mobile');
      }
    },
    quickAction(type) {
      this.handleNavClick();
      if (type === 'addTask') {
        this.$router.push('/tasks?action=new').catch(() => {});
      } else if (type === 'addRepo') {
        this.$router.push('/repositories?action=new').catch(() => {});
      }
    },
    handleUserCmd(cmd) {
      if (cmd === 'logout') return this.confirmLogout();
      if (cmd === 'toggleTheme') {
        this.$emit('toggle-theme');
        return;
      }
      if (cmd === 'refresh') {
        this.$message.success('正在刷新数据');
        this.$router.go(0);
        return;
      }
      if (cmd === 'about') {
        this.$alert('ResticManager 是一个基于 restic 的备份图形化运维控制台。\n版本 1.0.0', '关于 ResticManager', {
          confirmButtonText: '好的'
        });
      }
    },
    confirmLogout() {
      this.$confirm('确定要退出当前账号吗？', '确认登出', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }).then(() => {
        this.$store.dispatch('tasks/stop');
        authService.logout();
        this._lastUserRaw = null;
        this.$emit('close-mobile');
        this.$router.push('/login').catch(() => {});
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
  color: var(--sidebar-text);
  display: flex;
  flex-direction: column;
  z-index: 130;
  transition: width var(--transition-base), transform var(--transition-base), background-color var(--transition-base);
  border-right: 1px solid var(--sidebar-border);
}

.rm-sidebar--collapsed {
  width: var(--sidebar-width-collapsed);
}

.rm-sidebar__brand {
  height: var(--topbar-height);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  border-bottom: 1px solid var(--sidebar-border);
  flex-shrink: 0;
}

.rm-brand-link {
  display: flex;
  align-items: center;
  gap: 11px;
  text-decoration: none;
  min-width: 0;
}

.rm-logo {
  width: 34px;
  height: 34px;
  flex-shrink: 0;
  filter: drop-shadow(0 2px 8px rgba(6, 182, 212, 0.28));
}
.rm-logo svg {
  width: 100%;
  height: 100%;
}

.rm-brand-text {
  line-height: 1.2;
  min-width: 0;
}

.rm-brand-title {
  font-size: 15px;
  font-weight: 700;
  color: var(--color-text-primary);
  white-space: nowrap;
  letter-spacing: -0.01em;
}

.rm-brand-sub {
  font-size: 11px;
  color: var(--sidebar-muted);
  margin-top: 2px;
  white-space: nowrap;
}

.rm-sidebar__mobile-close {
  display: none;
  width: 30px;
  height: 30px;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.rm-sidebar__toggle {
  position: absolute;
  top: 17px;
  right: -12px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 1px solid var(--color-border-strong);
  background: var(--color-bg-card);
  color: var(--color-text-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  box-shadow: var(--shadow-sm);
  z-index: 10;
  transition: transform var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast), border-color var(--transition-fast);
}

.rm-sidebar__toggle:hover {
  transform: scale(1.1);
  background: var(--brand-600);
  border-color: var(--brand-600);
  color: #ffffff;
}

.rm-menu {
  flex: 1;
  padding: 14px 10px;
  overflow-y: auto;
  overflow-x: hidden;
}

.rm-menu__group--spaced {
  margin-top: 16px;
}

.rm-menu__group-title {
  font-size: 11px;
  color: var(--sidebar-muted);
  letter-spacing: 0.06em;
  padding: 0 12px 6px;
  font-weight: 600;
  user-select: none;
}

.rm-menu__divider {
  height: 1px;
  background: var(--sidebar-border);
  margin: 0 10px 10px;
}

.rm-menu__item {
  display: flex;
  align-items: center;
  gap: 11px;
  padding: 9px 12px;
  border-radius: var(--radius-md);
  border: 1px solid transparent;
  color: var(--sidebar-text);
  text-decoration: none;
  font-size: var(--font-sm);
  font-weight: 500;
  margin-bottom: 2px;
  position: relative;
  cursor: pointer;
  transition: background-color var(--transition-fast), color var(--transition-fast), border-color var(--transition-fast);
  user-select: none;
}

.rm-menu__item:hover {
  background: var(--color-bg-sidebar-hov);
  color: var(--color-text-primary);
}

.rm-menu__item--active {
  background: var(--sidebar-active-bg);
  border-color: var(--sidebar-active-bd);
  color: var(--sidebar-active-text);
  font-weight: 600;
}

.rm-menu__item--active:hover {
  background: var(--sidebar-active-bg);
  color: var(--sidebar-active-text);
}

.rm-menu__item--active::before {
  content: "";
  position: absolute;
  left: -10px;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 18px;
  background: var(--brand-400);
  border-radius: 0 3px 3px 0;
}

.rm-menu__icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.rm-menu__icon svg {
  width: 17px;
  height: 17px;
}

.rm-menu__label {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.rm-menu__badge {
  background: var(--brand-600);
  color: #ffffff;
  font-size: 11px;
  font-weight: 600;
  padding: 1px 7px;
  border-radius: var(--radius-full);
  font-variant-numeric: tabular-nums;
}

.rm-menu__dot {
  position: absolute;
  top: 8px;
  right: 9px;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--brand-400);
}

/* 底部快捷操作区 */
.rm-sidebar__quick {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  padding: 10px 10px 12px;
  border-top: 1px solid var(--sidebar-border);
}

.rm-sidebar--collapsed .rm-sidebar__quick {
  grid-template-columns: 1fr;
}

.rm-quick-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  height: 34px;
  padding: 0 8px;
  border-radius: var(--radius-md);
  border: 1px dashed var(--color-border-strong);
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  white-space: nowrap;
  transition: border-color var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast);
}

.rm-quick-btn:hover {
  border-style: solid;
  border-color: var(--brand-200);
  background: var(--brand-50);
  color: var(--brand-300);
}

/* 底部用户信息卡 */
.rm-sidebar__footer {
  border-top: 1px solid var(--sidebar-border);
  padding: 10px;
  display: flex;
  align-items: center;
  gap: 6px;
}

.rm-sidebar--collapsed .rm-sidebar__footer {
  flex-direction: column;
  gap: 8px;
}

.rm-user-card {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 8px;
  border-radius: var(--radius-md);
  min-width: 0;
  cursor: default;
  transition: background-color var(--transition-fast);
}

.rm-user-card:hover {
  background: var(--color-bg-sidebar-hov);
}

.rm-avatar {
  width: 32px;
  height: 32px;
  flex-shrink: 0;
  border-radius: 50%;
  background: linear-gradient(135deg, var(--brand-400), var(--brand-700));
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  font-weight: 700;
  font-size: 14px;
}

.rm-user-info {
  min-width: 0;
  flex: 1;
}

.rm-user-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.rm-user-role {
  font-size: 11px;
  color: var(--sidebar-muted);
}

.rm-user-trigger {
  width: 30px;
  height: 30px;
  border-radius: var(--radius-sm);
  border: none;
  background: transparent;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--sidebar-muted);
  cursor: pointer;
  transition: background-color var(--transition-fast), color var(--transition-fast);
}

.rm-user-trigger:hover {
  background: var(--color-bg-sidebar-hov);
  color: var(--color-text-primary);
}

.rm-user-trigger--icon {
  margin: 0 auto;
}

@media (max-width: 960px) {
  .rm-sidebar {
    width: var(--sidebar-width) !important;
    transform: translateX(-100%);
    box-shadow: var(--shadow-xl);
  }
  .rm-sidebar--mobile-open {
    transform: translateX(0);
  }
  .rm-sidebar__toggle {
    display: none;
  }
  .rm-sidebar__mobile-close {
    display: inline-flex;
  }
  .rm-sidebar--collapsed .rm-sidebar__quick {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .rm-sidebar--collapsed .rm-sidebar__footer {
    flex-direction: row;
  }
}
</style>