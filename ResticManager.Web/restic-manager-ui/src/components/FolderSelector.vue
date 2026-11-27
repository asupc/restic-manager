<template>
  <div class="folder-selector">
    <el-input v-model="selectedPath" :placeholder="placeholder" readonly @click.native="openDialog">
      <el-button slot="append" icon="el-icon-folder"></el-button>
    </el-input>

    <el-dialog :title="title" :visible.sync="dialogVisible" width="600px" :close-on-click-modal="false" append-to-body>
      <div class="folder-browser">
        <div class="path-bar">
          <el-button icon="el-icon-back" @click="goUp" :disabled="isAtRoot" size="small">上级目录</el-button>
          <div class="current-path" :title="displayPath">{{ displayPath }}</div>
          <el-button icon="el-icon-refresh" @click="refresh" size="small">刷新</el-button>
        </div>

        <el-table :data="folders" style="width: 100%; margin-top: 15px;" height="550" @row-dblclick="enterFolder">
          <el-table-column prop="name" label="名称">
            <template slot-scope="scope">
              <i :class="getIconClass(scope.row.type)"></i>
              {{ scope.row.name }}
            </template>
          </el-table-column>
          <!-- <el-table-column prop="type" label="类型" width="100"></el-table-column> -->
        </el-table>
      </div>

      <div slot="footer" class="dialog-footer">
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="selectCurrentFolder">选择此文件夹</el-button>
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
      folders: []
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
      try {
        const response = await backupService.getFolders(this.currentPath);
        this.folders = (response || []).map(item => ({
          name: item.name,
          type: item.type // folder, file or drive
        }));
      } catch (e) {
        if (fallbackPath !== undefined) {
          this.currentPath = fallbackPath;
        }
      }
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
        this.loadFolders(prevPath);
      } else if (row.type === 'drive') {
        this.currentPath = row.name;
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
    }
  }
};
</script>

<style scoped>
.folder-browser {
  height: 600px;
}

.path-bar {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: 15px;
  padding: 4px 6px;
  background-color: var(--color-bg-soft);
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
}

/* 上级目录/刷新作为路径条的内嵌按钮,与中间路径显示保持一体 */
.path-bar .el-button {
  background: transparent !important;
  border-color: transparent !important;
  color: var(--color-text-secondary) !important;
}

.path-bar .el-button:hover,
.path-bar .el-button:focus {
  background: var(--color-bg-sidebar-hov) !important;
  border-color: transparent !important;
  color: var(--brand-300) !important;
}

.path-bar .el-button.is-disabled,
.path-bar .el-button.is-disabled:hover,
.path-bar .el-button.is-disabled:focus {
  background: transparent !important;
  border-color: transparent !important;
  color: var(--color-text-placeholder) !important;
}

.current-path {
  flex: 1;
  min-width: 0;
  padding: 6px 4px;
  color: var(--color-text-regular);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-family: 'Courier New', monospace;
}

.dialog-footer {
  text-align: right;
}
</style>