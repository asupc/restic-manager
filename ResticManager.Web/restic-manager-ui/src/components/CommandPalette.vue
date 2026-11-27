<template>
  <transition name="cmd-fade">
    <div
      v-if="visible"
      class="cmd-overlay"
      role="dialog"
      aria-modal="true"
      aria-label="全局命令面板"
      @click.self="close"
    >
      <div class="cmd-dialog" @keydown.stop="handleKeydown">
        <!-- 搜索输入区 -->
        <div class="cmd-search">
          <svg class="cmd-search__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            ref="searchInput"
            v-model="keyword"
            type="text"
            class="cmd-search__input"
            placeholder="搜索页面或快捷操作（如：备份任务、快照、切换主题）..."
            autocomplete="off"
          />
          <button
            v-if="keyword"
            type="button"
            class="cmd-search__clear"
            title="清空"
            @click="keyword = ''"
          >
            <i class="el-icon-close"></i>
          </button>
          <kbd class="rm-kbd">Esc</kbd>
        </div>

        <!-- 结果列表 -->
        <div ref="listWrap" class="cmd-body">
          <div v-if="filteredItems.length === 0" class="cmd-empty">
            没有找到与 “{{ keyword }}” 匹配的页面或命令
          </div>

          <template v-else>
            <div
              v-for="group in groupedItems"
              :key="group.category"
              class="cmd-group"
            >
              <div class="cmd-group__title">{{ group.category }}</div>
              <div
                v-for="item in group.items"
                :key="item.id"
                :ref="'cmdItem_' + item.flatIndex"
                class="cmd-item"
                :class="{ 'cmd-item--active': item.flatIndex === activeIndex }"
                @mouseenter="activeIndex = item.flatIndex"
                @click="executeItem(item)"
              >
                <span class="cmd-item__icon" v-html="item.icon"></span>
                <div class="cmd-item__main">
                  <div class="cmd-item__title">{{ item.label }}</div>
                  <div class="cmd-item__desc">{{ item.description }}</div>
                </div>
                <span class="cmd-item__tag">{{ item.badge }}</span>
                <span v-if="item.flatIndex === activeIndex" class="cmd-item__enter">↵</span>
              </div>
            </div>
          </template>
        </div>

        <!-- 底部键盘提示 -->
        <div class="cmd-footer">
          <div class="cmd-footer__hints">
            <span><kbd class="rm-kbd">↑</kbd> <kbd class="rm-kbd">↓</kbd> 选择</span>
            <span><kbd class="rm-kbd">↵</kbd> 执行</span>
            <span><kbd class="rm-kbd">Esc</kbd> 关闭</span>
          </div>
          <div class="cmd-footer__count">{{ filteredItems.length }} 项可用</div>
        </div>
      </div>
    </div>
  </transition>
</template>

<script>
export default {
  name: 'CommandPalette',
  props: {
    visible: { type: Boolean, default: false },
    theme: { type: String, default: 'dark' }
  },
  data() {
    return {
      keyword: '',
      activeIndex: 0
    };
  },
  computed: {
    allCommands() {
      const nextThemeLabel = this.theme === 'dark' ? '浅色模式' : '深色模式';
      return [
        // 8 个主页面跳转
        {
          id: 'nav-home',
          category: '页面跳转',
          badge: '概览',
          label: '工作台',
          description: '查看系统健康状态、备份概览与近期动态',
          keywords: 'home dashboard 工作台 概览 首页 控制台',
          path: '/',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="9" rx="1.5"/><rect x="14" y="3" width="7" height="5" rx="1.5"/><rect x="14" y="12" width="7" height="9" rx="1.5"/><rect x="3" y="16" width="7" height="5" rx="1.5"/></svg>'
        },
        {
          id: 'nav-tasks',
          category: '页面跳转',
          badge: '备份中心',
          label: '备份任务',
          description: '管理定时备份计划与手动触发备份',
          keywords: 'tasks backup 备份任务 定时 计划 调度',
          path: '/tasks',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>'
        },
        {
          id: 'nav-records',
          category: '页面跳转',
          badge: '备份中心',
          label: '备份记录',
          description: '检索历史备份执行结果、耗时与统计信息',
          keywords: 'records history 备份记录 历史 执行 结果',
          path: '/records',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>'
        },
        {
          id: 'nav-rules',
          category: '页面跳转',
          badge: '备份中心',
          label: '备份规则',
          description: '配置包含路径、排除模式与快照保留策略',
          keywords: 'rules policy 备份规则 策略 过滤 忽略 保留',
          path: '/rules',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"/></svg>'
        },
        {
          id: 'nav-repositories',
          category: '页面跳转',
          badge: '存储与恢复',
          label: '备份仓库',
          description: '管理本地与对象存储仓库、校验与维护',
          keywords: 'repositories repo storage 备份仓库 存储 维护',
          path: '/repositories',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><path d="M3.27 6.96 12 12.01l8.73-5.05M12 22.08V12"/></svg>'
        },
        {
          id: 'nav-snapshots',
          category: '页面跳转',
          badge: '存储与恢复',
          label: '快照浏览',
          description: '浏览历史快照内容并执行文件恢复',
          keywords: 'snapshots restore backup-management 快照浏览 恢复 文件',
          path: '/backup-management',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>'
        },
        {
          id: 'nav-logs',
          category: '页面跳转',
          badge: '系统运维',
          label: '系统日志',
          description: '查看系统运行日志、告警与错误追踪',
          keywords: 'logs audit 系统日志 报错 调试 运维',
          path: '/logs',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 17 10 11 4 5"/><line x1="12" y1="19" x2="20" y2="19"/></svg>'
        },
        {
          id: 'nav-settings',
          category: '页面跳转',
          badge: '系统运维',
          label: '系统设置',
          description: '调整 Restic 路径、并发与系统参数',
          keywords: 'settings config 系统设置 配置 参数',
          path: '/settings',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>'
        },

        // 快捷操作
        {
          id: 'action-add-task',
          category: '快捷操作',
          badge: '操作',
          label: '新增备份任务',
          description: '直接前往备份任务页并打开新建表单',
          keywords: 'new add task create 新增备份任务 创建任务',
          path: '/tasks?action=new',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/></svg>'
        },
        {
          id: 'action-add-repo',
          category: '快捷操作',
          badge: '操作',
          label: '新建备份仓库',
          description: '直接前往备份仓库页并创建存储仓库',
          keywords: 'new add repo create 新建备份仓库 创建仓库',
          path: '/repositories?action=new',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/><line x1="12" y1="11" x2="12" y2="17"/><line x1="9" y1="14" x2="15" y2="14"/></svg>'
        },
        {
          id: 'action-toggle-theme',
          category: '快捷操作',
          badge: '外观',
          label: '切换深色 / 浅色主题',
          description: '立即切换为' + nextThemeLabel,
          keywords: 'theme dark light 切换深色 浅色 主题 外观 日间 夜间',
          action: 'toggleTheme',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"/><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/></svg>'
        },
        {
          id: 'action-refresh',
          category: '快捷操作',
          badge: '操作',
          label: '刷新当前页面',
          description: '重新加载当前页面数据',
          keywords: 'refresh reload 刷新 重载 页面',
          action: 'refresh',
          icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>'
        }
      ];
    },
    filteredItems() {
      const q = (this.keyword || '').trim().toLowerCase();
      if (!q) return this.allCommands;
      return this.allCommands.filter(item => {
        const haystack = [item.label, item.description, item.category, item.badge, item.keywords]
          .join(' ')
          .toLowerCase();
        return haystack.indexOf(q) !== -1;
      });
    },
    groupedItems() {
      const map = {};
      const order = [];
      this.filteredItems.forEach((item, idx) => {
        if (!map[item.category]) {
          map[item.category] = [];
          order.push(item.category);
        }
        map[item.category].push(Object.assign({}, item, { flatIndex: idx }));
      });
      return order.map(category => ({
        category,
        items: map[category]
      }));
    }
  },
  watch: {
    visible(val) {
      if (val) {
        this.keyword = '';
        this.activeIndex = 0;
        this.$nextTick(() => {
          if (this.$refs.searchInput) {
            this.$refs.searchInput.focus();
          }
        });
      }
    },
    keyword() {
      this.activeIndex = 0;
    }
  },
  methods: {
    close() {
      this.$emit('update:visible', false);
    },
    handleKeydown(e) {
      const key = e && e.key ? e.key : '';
      if (key === 'Escape') {
        e.preventDefault();
        this.close();
        return;
      }
      const total = this.filteredItems.length;
      if (total === 0) return;

      if (key === 'ArrowDown') {
        e.preventDefault();
        this.activeIndex = (this.activeIndex + 1) % total;
        this.scrollActiveIntoView();
      } else if (key === 'ArrowUp') {
        e.preventDefault();
        this.activeIndex = (this.activeIndex - 1 + total) % total;
        this.scrollActiveIntoView();
      } else if (key === 'Enter') {
        e.preventDefault();
        const target = this.filteredItems[this.activeIndex];
        if (target) {
          this.executeItem(target);
        }
      }
    },
    scrollActiveIntoView() {
      this.$nextTick(() => {
        const refEntry = this.$refs['cmdItem_' + this.activeIndex];
        const el = Array.isArray(refEntry) ? refEntry[0] : refEntry;
        if (el && typeof el.scrollIntoView === 'function') {
          el.scrollIntoView({ block: 'nearest' });
        }
      });
    },
    executeItem(item) {
      if (!item) return;
      this.close();
      if (item.path) {
        this.$router.push(item.path).catch(() => {});
        return;
      }
      if (item.action === 'toggleTheme') {
        this.$emit('toggle-theme');
        return;
      }
      if (item.action === 'refresh') {
        this.$router.go(0);
      }
    }
  }
};
</script>

<style scoped>
.cmd-overlay {
  position: fixed;
  inset: 0;
  z-index: 2000;
  background: var(--color-bg-backdrop);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 12vh 16px 24px;
}

.cmd-dialog {
  width: 100%;
  max-width: 580px;
  background: var(--color-bg-card);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-xl);
  box-shadow: var(--shadow-xl);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.cmd-search {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--color-border);
}

.cmd-search__icon {
  width: 18px;
  height: 18px;
  color: var(--brand-400);
  flex-shrink: 0;
}

.cmd-search__input {
  flex: 1;
  min-width: 0;
  border: none;
  outline: none;
  background: transparent;
  color: var(--color-text-primary);
  font-size: var(--font-md);
  font-family: inherit;
}

.cmd-search__input::placeholder {
  color: var(--color-text-placeholder);
}

.cmd-search__clear {
  width: 22px;
  height: 22px;
  border: none;
  border-radius: 50%;
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.cmd-search__clear:hover {
  background: var(--color-bg-hover);
  color: var(--color-text-primary);
}

.cmd-body {
  max-height: 380px;
  overflow-y: auto;
  padding: 8px;
}

.cmd-empty {
  padding: 36px 16px;
  text-align: center;
  color: var(--color-text-secondary);
  font-size: var(--font-sm);
}

.cmd-group + .cmd-group {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--color-border-soft);
}

.cmd-group__title {
  padding: 4px 10px 6px;
  font-size: 11px;
  font-weight: 600;
  color: var(--color-text-placeholder);
  letter-spacing: 0.05em;
}

.cmd-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  border: 1px solid transparent;
  cursor: pointer;
  transition: background-color var(--transition-fast), border-color var(--transition-fast);
}

.cmd-item--active {
  background: var(--color-bg-hover);
  border-color: var(--brand-200);
}

.cmd-item__icon {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-sm);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.cmd-item--active .cmd-item__icon {
  background: var(--brand-50);
  border-color: var(--brand-200);
  color: var(--brand-400);
}

.cmd-item__icon svg {
  width: 16px;
  height: 16px;
}

.cmd-item__main {
  flex: 1;
  min-width: 0;
}

.cmd-item__title {
  font-size: var(--font-sm);
  font-weight: 600;
  color: var(--color-text-primary);
  line-height: 1.3;
}

.cmd-item__desc {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cmd-item__tag {
  font-size: 11px;
  color: var(--color-text-secondary);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  padding: 2px 7px;
  border-radius: var(--radius-full);
  white-space: nowrap;
  flex-shrink: 0;
}

.cmd-item__enter {
  font-size: 12px;
  font-weight: 600;
  color: var(--brand-400);
  flex-shrink: 0;
}

.cmd-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background: var(--color-bg-soft);
  border-top: 1px solid var(--color-border);
  font-size: 11px;
  color: var(--color-text-secondary);
}

.cmd-footer__hints {
  display: flex;
  align-items: center;
  gap: 14px;
}

.cmd-footer__hints span {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.cmd-footer__count {
  font-variant-numeric: tabular-nums;
}

.cmd-fade-enter-active,
.cmd-fade-leave-active {
  transition: opacity 160ms var(--ease-out);
}
.cmd-fade-enter-active .cmd-dialog,
.cmd-fade-leave-active .cmd-dialog {
  transition: transform 180ms var(--ease-out), opacity 160ms var(--ease-out);
}
.cmd-fade-enter,
.cmd-fade-leave-to {
  opacity: 0;
}
.cmd-fade-enter .cmd-dialog,
.cmd-fade-leave-to .cmd-dialog {
  transform: translateY(-10px) scale(0.98);
  opacity: 0;
}
</style>
