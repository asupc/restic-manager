<template>
  <div class="folder-selector">
    <el-input v-model="selectedPath" :placeholder="placeholder" readonly @click.native="openDialog">
      <el-button slot="append" icon="el-icon-folder"></el-button>
    </el-input>

    <el-dialog :title="title" :visible.sync="dialogVisible" width="660px" :close-on-click-modal="false" append-to-body>
      <div class="folder-browser">
        <!-- 顶部可点击路径面包屑栏 -->
        <div class="path-bar">
          <el-tooltip content="返回上级目录" placement="top" :open-delay="300">
            <el-button icon="el-icon-back" @click="goUp" :disabled="isAtRoot" size="mini" class="path-bar__btn"></el-button>
          </el-tooltip>

          <div class="breadcrumb-track" :title="displayPath">
            <template v-for="(crumb, idx) in breadcrumbs">
              <span v-if="idx > 0" :key="'sep-' + idx" class="breadcrumb-sep">
                <i class="el-icon-arrow-right"></i>
              </span>
              <button
                :key="'crumb-' + idx"
                type="button"
                class="breadcrumb-item"
                :class="{ 'is-current': idx === breadcrumbs.length - 1 }"
                :disabled="idx === breadcrumbs.length - 1"
                @click="navigateToBreadcrumb(crumb.path)"
              >
                <i v-if="crumb.icon" :class="crumb.icon" class="breadcrumb-item__icon"></i>
                <span>{{ crumb.label }}</span>
              </button>
            </template>
          </div>

          <el-tooltip content="刷新当前目录" placement="top" :open-delay="300">
            <el-button icon="el-icon-refresh" @click="refresh" :loading="loading" size="mini" class="path-bar__btn"></el-button>
          </el-tooltip>
        </div>

        <!-- 当前目录实时搜索栏 -->
        <div class="filter-bar">
          <el-input
            v-model="filterKeyword"
            placeholder="按名称快速筛选当前目录..."
            prefix-icon="el-icon-search"
            size="small"
            clearable
            class="filter-bar__input"
          ></el-input>
          <span class="filter-bar__count">
            <template v-if="filterKeyword.trim()">
              匹配 {{ filteredFolders.length }} / {{ folders.length }} 项
            </template>
            <template v-else>
              共 {{ folders.length }} 项
            </template>
          </span>
        </div>

        <!-- 目录卡片列表 -->
        <div class="folder-list" v-loading="loading">
          <div v-if="!loading && filteredFolders.length === 0" class="folder-empty">
            <i :class="filterKeyword.trim() ? 'el-icon-search' : 'el-icon-folder-opened'" class="folder-empty__icon"></i>
            <div class="folder-empty__title">
              {{ filterKeyword.trim() ? '未找到匹配的项目' : '当前目录为空' }}
            </div>
            <div class="folder-empty__desc" v-if="filterKeyword.trim()">
              请尝试修改搜索关键词
            </div>
          </div>

          <div
            v-for="item in filteredFolders"
            :key="item.type + '::' + item.name"
            class="folder-row"
            :class="{
              'is-active': activeItemName === item.name,
              'is-navigable': item.type === 'folder' || item.type === 'drive'
            }"
            @click="onRowClick(item)"
            @dblclick="enterFolder(item)"
          >
            <span class="folder-row__icon-wrap" :class="`folder-row__icon-wrap--${getIconTone(item.type)}`">
              <i :class="getIconClass(item.type)"></i>
            </span>
            <span class="folder-row__name" :title="item.name">{{ item.name }}</span>
            <span class="folder-row__type">{{ getTypeLabel(item.type) }}</span>
            <button
              v-if="item.type === 'folder' || item.type === 'drive'"
              type="button"
              class="folder-row__enter"
              title="进入此目录"
              @click.stop="enterFolder(item)"
            >
              <i class="el-icon-arrow-right"></i>
            </button>
          </div>
        </div>
      </div>

      <!-- 底部当前将选择的路径与操作按钮 -->
      <div slot="footer" class="dialog-footer">
        <div class="footer-preview" :title="previewSelectedPath">
          <span class="footer-preview__label">当前将选择</span>
          <span class="footer-preview__path">{{ previewSelectedPath }}</span>
        </div>
        <div class="footer-actions">
          <el-button size="small" @click="dialogVisible = false">取消</el-button>
          <el-button size="small" type="primary" @click="selectCurrentFolder">选择此文件夹</el-button>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script>
import { backupService } from '../services/backup.service';

export default {
  name: 'FolderSelector',
  props: {
    value: {
      type: String,
      default: ''
    },
    title: {
      type: String,
      default: '选择文件夹'
    },
    placeholder: {
      type: String,
      default: '点击选择文件夹'
    },
    type: {
      type: String,
      default: 'folder' // folder or file
    }
  },
  data() {
    return {
      dialogVisible: false,
      currentPath: '',
      selectedPath: '',
      folders: [],
      filterKeyword: '',
      activeItemName: '',
      loading: false
    };
  },
  computed: {
    isAtRoot() {
      // 在根目录（驱动器列表或空路径）
      // Linux系统上，根目录是 "/"
      // Windows系统上，根目录是驱动器列表（空字符串）
      return !this.currentPath || this.currentPath === '' || this.currentPath === '/';
    },

    displayPath() {
      if (!this.currentPath) return '根目录';
      return this.currentPath;
    },

    previewSelectedPath() {
      if (!this.currentPath) return '根目录';
      let normalized = this.normalizePath(this.currentPath);
      if (normalized && !normalized.startsWith('/') && !normalized.match(/^[a-zA-Z]:/)) {
        normalized = '/' + normalized;
      }
      return normalized || '根目录';
    },

    breadcrumbs() {
      const p = this.currentPath || '';
      if (!p) {
        return [{ label: '根目录', path: '', icon: 'el-icon-s-home' }];
      }

      // Windows 路径（如 C:\ 或 C:\Users\Admin）
      if (/^[a-zA-Z]:/.test(p)) {
        const crumbs = [{ label: '计算机', path: '', icon: 'el-icon-monitor' }];
        const normalized = p.replace(/\//g, '\\').replace(/\\+$/, '');
        const parts = normalized.split('\\').filter(Boolean);
        if (parts.length > 0) {
          const drive = parts[0] + '\\';
          crumbs.push({ label: drive, path: drive, icon: 'el-icon-box' });
          let acc = parts[0];
          for (let i = 1; i < parts.length; i++) {
            acc = acc + '\\' + parts[i];
            crumbs.push({ label: parts[i], path: acc, icon: '' });
          }
        }
        return crumbs;
      }

      // Linux / Unix 路径（如 / 或 /var/log/nginx）
      const unixNorm = p.replace(/\\/g, '/');
      const crumbs = [{ label: '/', path: '/', icon: 'el-icon-s-home' }];
      const parts = unixNorm.split('/').filter(Boolean);
      let acc = '';
      for (let i = 0; i < parts.length; i++) {
        acc = acc + '/' + parts[i];
        crumbs.push({ label: parts[i], path: acc, icon: '' });
      }
      return crumbs;
    },

    filteredFolders() {
      const q = (this.filterKeyword || '').trim().toLowerCase();
      if (!q) return this.folders;
      return this.folders.filter(item => item.name && item.name.toLowerCase().includes(q));
    }
  },

  watch: {
    value: {
      handler(newVal) {
        this.selectedPath = newVal || '';
        // 同时更新currentPath以确保在打开对话框时显示正确的路径
        if (!this.dialogVisible) {
          this.currentPath = newVal || '';
        }
      },
      immediate: true
    }
  },

  mounted() {
    // 组件挂载时确保路径正确初始化
    if (this.value) {
      this.selectedPath = this.value;
      this.currentPath = this.value;
    }
  },

  activated() {
    // 当组件被激活时，确保路径正确初始化
    if (this.value && !this.selectedPath) {
      this.selectedPath = this.value;
      this.currentPath = this.value;
    }
  },
  methods: {
    openDialog() {
      this.dialogVisible = true;
      this.filterKeyword = '';
      this.activeItemName = '';
      // 确保使用传入的value作为初始路径
      this.currentPath = this.value || this.selectedPath || '';
      if (!this.currentPath && this.value) {
        this.currentPath = this.value;
      }
      this.loadFolders();
    },

    normalizePath(path) {
      // 标准化路径分隔符
      if (!path) return path;

      // Windows驱动器根目录特殊处理（path 已含冒号，如 D: -> D:\）
      if (path.match(/^[a-zA-Z]:$/)) {
        return path + '\\';
      }

      // Windows路径处理
      if (path.match(/^[a-zA-Z]:\\/)) {
        return path; // 已经是正确的Windows路径格式
      } else if (path.match(/^[a-zA-Z]:/)) {
        return path.replace(/\//g, '\\') + '\\';
      }

      // Unix/Linux路径处理
      return path.replace(/\\/g, '/');
    },

    async loadFolders(fallbackPath) {
      this.loading = true;
      try {
        const response = await backupService.getFolders(this.currentPath);
        this.folders = (response || []).map(item => ({
          name: item.name,
          type: item.type // folder, file or drive
        }));
        this.activeItemName = '';
      } catch (e) {
        if (fallbackPath !== undefined) {
          this.currentPath = fallbackPath;
        }
      } finally {
        this.loading = false;
      }
    },

    onRowClick(item) {
      this.activeItemName = item.name;
    },

    navigateToBreadcrumb(targetPath) {
      if (targetPath === this.currentPath) return;
      const prevPath = this.currentPath;
      this.currentPath = targetPath;
      this.filterKeyword = '';
      this.loadFolders(prevPath);
    },

    enterFolder(row) {
      const prevPath = this.currentPath;
      if (row.type === 'folder') {
        // 处理文件夹路径
        if (this.currentPath) {
          // 检查是否是Windows驱动器根目录（如 D:\）
          if (this.currentPath.match(/^[a-zA-Z]:\\$/)) {
            this.currentPath = this.currentPath + row.name;
          } else if (this.currentPath.includes('\\')) {
            // Windows路径
            this.currentPath = this.currentPath.replace(/\\+$/, '') + '\\' + row.name;
          } else {
            // Unix/Linux路径
            // 如果当前路径是根目录(/)，则不需要再添加/
            if (this.currentPath === '/') {
              this.currentPath = this.currentPath + row.name;
            } else {
              this.currentPath = this.currentPath.replace(/\/+$/, '') + '/' + row.name;
            }
          }
        } else {
          // 在根目录下选择文件夹，需要添加前导斜杠
          if (!row.name.startsWith('/') && !row.name.match(/^[a-zA-Z]:/)) {
            this.currentPath = '/' + row.name;
          } else {
            this.currentPath = row.name;
          }
        }
        this.filterKeyword = '';
        this.loadFolders(prevPath);
      } else if (row.type === 'drive') {
        this.currentPath = row.name;
        this.filterKeyword = '';
        this.loadFolders(prevPath);
      }
    },

    goUp() {
      const prevPath = this.currentPath;
      if (this.currentPath) {
        // 处理Windows驱动器根目录情况（如 D:\ 或 D:）
        if (this.currentPath.match(/^[a-zA-Z]:\\?$/)) {
          // 返回Windows驱动器列表
          this.currentPath = '';
          this.filterKeyword = '';
          this.loadFolders(prevPath);
          return;
        }

        // 处理Windows路径
        if (this.currentPath.includes('\\')) {
          const trimmed = this.currentPath.replace(/\\+$/, '');
          if (trimmed.match(/^[a-zA-Z]:$/)) {
            this.currentPath = '';
          } else {
            const parts = trimmed.split('\\');
            if (parts.length > 1) {
              parts.pop();
              let parent = parts.join('\\');
              // 确保驱动器根目录格式为 D:\（避免拼出双冒号）
              if (parent.match(/^[a-zA-Z]:$/)) {
                parent += '\\';
              }
              this.currentPath = parent;
            } else {
              this.currentPath = '';
            }
          }
        } else {
          // Unix/Linux路径处理，先清理尾斜杠
          const trimmed = this.currentPath.replace(/\/+$/, '');
          if (!trimmed) {
            this.currentPath = '';
          } else {
            const parts = trimmed.split('/');
            if (parts.length > 2) {
              parts.pop();
              this.currentPath = parts.join('/');
            } else if (parts.length === 2 && parts[1] !== '') {
              // 只有一级目录，如 /home，返回根目录
              this.currentPath = '/';
            } else {
              this.currentPath = '';
            }
          }
        }
        this.filterKeyword = '';
        this.loadFolders(prevPath);
      } else {
        // 已经在根目录，保持不变
        this.loadFolders(prevPath);
      }
    },

    navigateToPath() {
      this.loadFolders();
    },

    refresh() {
      this.loadFolders();
    },

    selectCurrentFolder() {
      // 标准化路径
      let normalizedPath = this.normalizePath(this.currentPath);

      // 确保Linux系统上的路径以/开头
      if (normalizedPath && !normalizedPath.startsWith('/') && !normalizedPath.match(/^[a-zA-Z]:/)) {
        normalizedPath = '/' + normalizedPath;
      }

      this.selectedPath = normalizedPath;
      this.$emit('input', this.selectedPath);
      this.dialogVisible = false;
    },

    getIconClass(type) {
      switch (type) {
        case 'folder':
          return 'el-icon-folder';
        case 'drive':
          return 'el-icon-box';
        default:
          return 'el-icon-document';
      }
    },

    getIconTone(type) {
      if (type === 'folder') return 'folder';
      if (type === 'drive') return 'drive';
      return 'file';
    },

    getTypeLabel(type) {
      if (type === 'folder') return '文件夹';
      if (type === 'drive') return '驱动器';
      return '文件';
    }
  }
};
</script>

<style scoped>
.folder-browser {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* 路径面包屑栏 */
.path-bar {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  background-color: var(--color-bg-soft);
  border-radius: var(--radius-md, 10px);
  border: 1px solid var(--color-border);
}

.path-bar__btn {
  background: transparent !important;
  border-color: transparent !important;
  color: var(--color-text-secondary) !important;
  padding: 6px 8px !important;
  flex-shrink: 0;
}

.path-bar__btn:hover,
.path-bar__btn:focus {
  background: var(--color-bg-card) !important;
  border-color: var(--color-border) !important;
  color: var(--brand-400) !important;
}

.path-bar__btn.is-disabled,
.path-bar__btn.is-disabled:hover {
  background: transparent !important;
  border-color: transparent !important;
  color: var(--color-text-placeholder, var(--color-text-secondary)) !important;
  opacity: 0.45;
}

.breadcrumb-track {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 2px;
  overflow-x: auto;
  padding: 2px 4px;
  scrollbar-width: thin;
}

.breadcrumb-sep {
  color: var(--color-text-secondary);
  font-size: 11px;
  opacity: 0.65;
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
}

.breadcrumb-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  border: 1px solid transparent;
  border-radius: var(--radius-sm, 6px);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  white-space: nowrap;
  cursor: pointer;
  flex-shrink: 0;
  transition: color 150ms ease, background-color 150ms ease, border-color 150ms ease;
}

.breadcrumb-item:hover:not(:disabled) {
  color: var(--brand-400);
  background: var(--brand-50);
  border-color: var(--brand-200, var(--color-border));
}

.breadcrumb-item.is-current {
  color: var(--color-text-primary);
  font-weight: 600;
  background: var(--color-bg-card);
  border-color: var(--color-border);
  cursor: default;
}

.breadcrumb-item__icon {
  font-size: 12px;
}

/* 当前目录搜索栏 */
.filter-bar {
  display: flex;
  align-items: center;
  gap: 10px;
}

.filter-bar__input {
  flex: 1;
}

.filter-bar__count {
  font-size: 12px;
  color: var(--color-text-secondary);
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}

/* 卡片行目录列表 */
.folder-list {
  height: 410px;
  overflow-y: auto;
  padding: 8px;
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 10px);
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.folder-empty {
  margin: auto;
  text-align: center;
  padding: 48px 16px;
  color: var(--color-text-secondary);
}

.folder-empty__icon {
  font-size: 30px;
  margin-bottom: 8px;
  display: inline-block;
  opacity: 0.7;
}

.folder-empty__title {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.folder-empty__desc {
  font-size: 12px;
  margin-top: 4px;
  color: var(--color-text-secondary);
}

.folder-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border-radius: var(--radius-sm, 6px);
  background: var(--color-bg-card);
  border: 1px solid transparent;
  cursor: default;
  user-select: none;
  transition: background-color 150ms ease, border-color 150ms ease, transform 150ms ease;
}

.folder-row.is-navigable {
  cursor: pointer;
}

.folder-row:hover {
  border-color: var(--brand-500);
  background: var(--brand-50);
}

.folder-row.is-active {
  border-color: var(--brand-400);
  background: var(--brand-100, var(--brand-50));
}

.folder-row__icon-wrap {
  width: 28px;
  height: 28px;
  border-radius: var(--radius-sm, 6px);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 15px;
  flex-shrink: 0;
}

.folder-row__icon-wrap--folder {
  background: var(--warning-50);
  color: var(--warning-500);
}

.folder-row__icon-wrap--drive {
  background: var(--brand-50);
  color: var(--brand-400);
}

.folder-row__icon-wrap--file {
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
}

.folder-row__name {
  flex: 1;
  min-width: 0;
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.folder-row__type {
  font-size: 11px;
  color: var(--color-text-secondary);
  padding: 2px 7px;
  border-radius: var(--radius-full, 9999px);
  background: var(--color-bg-soft);
  flex-shrink: 0;
}

.folder-row__enter {
  width: 24px;
  height: 24px;
  border: 0;
  border-radius: var(--radius-xs, 4px);
  background: transparent;
  color: var(--color-text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  flex-shrink: 0;
  transition: color 150ms ease, background-color 150ms ease;
}

.folder-row:hover .folder-row__enter,
.folder-row__enter:hover {
  color: var(--brand-400);
  background: var(--color-bg-card);
}

/* 底部路径预览与按钮 */
.dialog-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.footer-preview {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  flex: 1;
  text-align: left;
}

.footer-preview__label {
  font-size: 12px;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.footer-preview__path {
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  color: var(--color-text-primary);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-xs, 4px);
  padding: 3px 8px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.footer-actions {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}
</style>