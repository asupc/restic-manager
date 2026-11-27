<template>
  <div class="snapshot-files-page">
    <!-- 顶部精致信息卡 -->
    <div class="rm-surface files-hero">
      <div class="files-hero__top">
        <div class="files-hero__identity">
          <el-button size="small" icon="el-icon-arrow-left" @click="goBack">返回</el-button>
          <span class="rm-code-chip files-hero__snap-chip" :title="snapshotFullId">
            <i class="el-icon-camera"></i>
            <span>{{ snapshotShortId || '快照' }}</span>
            <button
              type="button"
              class="rm-code-chip__copy"
              title="复制快照 ID"
              @click="copySnapshotId"
            >
              <i class="el-icon-document-copy"></i>
            </button>
          </span>
          <span v-if="repository && repository.name" class="files-hero__repo">
            <i class="el-icon-coin"></i>
            {{ repository.name }}
          </span>
          <span class="rm-status-pill rm-status-pill--neutral">
            <span class="text-mono">{{ filteredFiles.length }}</span> 项
          </span>
        </div>

        <div class="files-hero__tools">
          <el-input
            v-model="searchKeyword"
            placeholder="即时过滤当前文件名..."
            prefix-icon="el-icon-search"
            size="small"
            clearable
            class="files-hero__search"
          ></el-input>
          <el-button
            size="small"
            icon="el-icon-refresh"
            :loading="loading"
            @click="reloadCurrentDirectory"
          >
            刷新
          </el-button>
        </div>
      </div>

      <!-- 可点击路径面包屑 -->
      <div class="path-breadcrumb">
        <span class="path-breadcrumb__label">
          <i class="el-icon-folder-opened"></i>
          当前目录
        </span>
        <div class="path-breadcrumb__track">
          <button
            type="button"
            class="path-crumb"
            :class="{ 'path-crumb--active': pathSegments.length === 0 }"
            @click="navigateToPath('')"
          >
            <i class="el-icon-house"></i>
            <span>根目录</span>
          </button>
          <template v-for="(seg, idx) in pathSegments">
            <i :key="'sep-' + idx" class="el-icon-arrow-right path-crumb__sep"></i>
            <button
              :key="'seg-' + idx"
              type="button"
              class="path-crumb text-mono"
              :class="{ 'path-crumb--active': idx === pathSegments.length - 1 }"
              :title="seg.path"
              @click="navigateToPath(seg.path)"
            >
              {{ seg.label }}
            </button>
          </template>
        </div>
      </div>
    </div>

    <!-- 文件树表格 -->
    <div class="rm-surface files-surface" v-loading="loading">
      <div v-if="filteredFiles.length === 0 && !loading" class="empty">
        <i class="el-icon-folder-delete empty__icon"></i>
        <div class="empty__title">{{ searchKeyword ? '未找到匹配的文件' : '该目录下没有文件' }}</div>
        <div class="empty__desc">
          {{ searchKeyword ? '尝试更换或清空筛选关键词' : '点击上方路径面包屑可返回上级目录' }}
        </div>
        <el-button
          v-if="searchKeyword"
          size="small"
          type="primary"
          plain
          class="empty__action"
          @click="searchKeyword = ''"
        >
          清空过滤
        </el-button>
      </div>

      <el-table
        v-else
        :key="tableRenderKey"
        :data="filteredFiles"
        row-key="path"
        :tree-props="{ children: 'children', hasChildren: 'hasChildren' }"
        lazy
        :load="loadSubdirectory"
        size="small"
        :row-class-name="'file-row'"
        :cell-class-name="'file-cell-tight'"
      >
        <el-table-column label="名称" min-width="300">
          <template slot-scope="scope">
            <div class="file-cell">
              <span
                class="file-cell__icon-wrap"
                :class="scope.row.type === 'dir' ? 'file-cell__icon-wrap--folder' : 'file-cell__icon-wrap--file'"
              >
                <i :class="scope.row.type === 'dir' ? 'el-icon-folder' : 'el-icon-document'"></i>
              </span>
              <span
                class="file-cell__name"
                :class="{ 'file-cell__name--dir': scope.row.type === 'dir' }"
                :title="scope.row.path"
              >
                {{ scope.row.name }}
              </span>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="大小" width="120" align="right">
          <template slot-scope="scope">
            <span class="text-mono file-cell__size" v-if="scope.row.type === 'file'">
              {{ formatFileSize(scope.row.size) }}
            </span>
            <span v-else class="text-muted">—</span>
          </template>
        </el-table-column>

        <el-table-column label="权限" width="130" align="center">
          <template slot-scope="scope">
            <span v-if="scope.row.permissions" class="rm-code-chip file-cell__perm">
              {{ scope.row.permissions }}
            </span>
            <span v-else class="text-muted">—</span>
          </template>
        </el-table-column>

        <el-table-column label="修改时间" width="175" align="center">
          <template slot-scope="scope">
            <span v-if="scope.row.mtime" class="text-mono text-secondary file-cell__mtime">
              {{ formatMtime(scope.row.mtime) }}
            </span>
            <span v-else class="text-muted">—</span>
          </template>
        </el-table-column>

        <!-- 树形表格下 fixed 列的克隆表行高与主表不一致会逐行错位，且本表无横向滚动，不做固定列 -->
        <el-table-column label="操作" width="150" align="center">
          <template slot-scope="scope">
            <div class="file-actions">
              <el-button
                v-if="scope.row.type === 'file'"
                type="text"
                size="mini"
                icon="el-icon-download"
                :loading="downloadingPath === scope.row.path"
                @click="downloadFile(scope.row)"
              >
                下载
              </el-button>
              <el-button
                type="text"
                size="mini"
                icon="el-icon-refresh-left"
                @click="restoreFile(scope.row)"
              >
                恢复
              </el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 恢复对话框 -->
    <el-dialog
      :visible.sync="restoreDialogVisible"
      title="恢复文件或目录"
      width="520px"
      :close-on-click-modal="false"
      append-to-body
    >
      <div v-if="restoreItem" class="restore-target-box">
        <span
          class="file-cell__icon-wrap"
          :class="restoreItem.type === 'dir' ? 'file-cell__icon-wrap--folder' : 'file-cell__icon-wrap--file'"
        >
          <i :class="restoreItem.type === 'dir' ? 'el-icon-folder' : 'el-icon-document'"></i>
        </span>
        <div class="restore-target-box__meta">
          <div class="restore-target-box__name">{{ restoreItem.name }}</div>
          <div class="restore-target-box__path text-mono">{{ restoreItem.path }}</div>
        </div>
      </div>

      <el-form :model="restoreForm" label-position="top">
        <el-form-item label="恢复目标目录">
          <folder-selector
            v-model="restoreForm.path"
            title="选择恢复路径"
            placeholder="选择或输入恢复的目标目录"
          ></folder-selector>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            选定内容将按原有结构恢复到该目标目录下
          </div>
        </el-form-item>

        <el-form-item class="restore-verify-item">
          <div class="restore-verify-box">
            <el-checkbox v-model="restoreForm.verify">恢复后校验文件内容（--verify）</el-checkbox>
            <div class="restore-verify-box__desc">开启后会逐块校验恢复后的文件完整性，耗时略增</div>
          </div>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="restoreDialogVisible = false" :disabled="restoring">取消</el-button>
        <el-button type="primary" icon="el-icon-refresh-left" :loading="restoring" @click="performRestore">
          开始恢复
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { Message } from 'element-ui';
import { repositoryService } from '@/services/repository.service';
import { pollTask } from '@/utils/task-poller';
import FolderSelector from '@/components/FolderSelector.vue';

export default {
  name: 'SnapshotFiles',
  components: { FolderSelector },
  data() {
    return {
      repository: null,
      snapshotShortId: '',
      files: [],
      searchKeyword: '',
      loading: false,
      restoreDialogVisible: false,
      restoreItem: null,
      restoreForm: { path: '', verify: false },
      currentPath: '',
      downloadingPath: '',
      restoring: false,
      tableRenderKey: 0
    };
  },
  computed: {
    snapshotFullId() {
      return this.$route.params.snapshotId || this.snapshotShortId || '';
    },
    filteredFiles() {
      if (!this.searchKeyword) return this.files;
      const q = this.searchKeyword.toLowerCase();
      return this.files.filter(f => (f.name || '').toLowerCase().includes(q));
    },
    pathSegments() {
      const raw = (this.currentPath || '').trim();
      if (!raw || raw === '/' || raw === '\\') return [];
      const hasBackslash = raw.indexOf('\\') !== -1 && raw.indexOf('/') === -1;
      const sep = hasBackslash ? '\\' : '/';
      const isLeadingSlash = raw.startsWith('/');
      const parts = raw.split(/[\\/]+/).filter(Boolean);
      const segments = [];
      let acc = '';
      for (let i = 0; i < parts.length; i++) {
        const part = parts[i];
        if (i === 0) {
          if (isLeadingSlash) {
            acc = '/' + part;
          } else if (hasBackslash && /^[a-zA-Z]:$/.test(part)) {
            acc = part + '\\';
          } else {
            acc = part;
          }
        } else {
          acc = acc.endsWith(sep) ? acc + part : acc + sep + part;
        }
        segments.push({ label: part, path: acc });
      }
      return segments;
    }
  },
  created() {
    this._fetchSeq = 0;
  },
  methods: {
    goBack() { this.$router.go(-1); },
    async copySnapshotId() {
      const text = this.snapshotFullId || this.snapshotShortId;
      if (!text) return;
      try {
        await navigator.clipboard.writeText(text);
        Message.success('快照 ID 已复制到剪贴板');
      } catch (e) {
        Message.info('请手动复制快照 ID');
      }
    },
    // 提交文件树后台任务并轮询到终态，返回解析后的节点数组（失败/取消返回 null）
    async fetchTreeNodes(repositoryId, snapshotId, path) {
      let res;
      try {
        res = await repositoryService.getSnapshotSubdirectoryFilesTree(repositoryId, snapshotId, path);
      } catch (e) {
        Message.error(e.response?.data?.message || e.message || '');
        return null;
      }
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      try {
        const task = await pollTask(res.taskId, { shouldStop: () => this._destroyed });
        if (this._destroyed) return null;
        if (task.status === 'Succeeded') {
          return task.resultJson ? JSON.parse(task.resultJson) : [];
        }
        Message.error('加载文件失败: ' + (task.error || ''));
        return null;
      } catch (e) {
        if (e.message !== 'cancelled') Message.error('加载文件失败: ' + (e.message || ''));
        return null;
      }
    },
    async fetchFiles() {
      const seq = ++this._fetchSeq;
      this.loading = true;
      try {
        const repositoryId = this.$route.params.repositoryId;
        const snapshotId = this.$route.params.snapshotId;
        this.snapshotShortId = this.$route.query.shortId || snapshotId.substring(0, 8);
        this.currentPath = this.$route.query.path || '';
        this.repository = await repositoryService.getRepository(repositoryId);
        if (this._destroyed || seq !== this._fetchSeq) return;
        const nodes = await this.fetchTreeNodes(repositoryId, snapshotId, this.currentPath);
        if (this._destroyed || seq !== this._fetchSeq) return;
        if (nodes !== null) {
          this.files = nodes;
          this.tableRenderKey += 1;
        }
      } catch (e) {
        if (this._destroyed || seq !== this._fetchSeq) return;
        Message.error('加载文件失败: ' + e.message);
      } finally {
        if (!this._destroyed && seq === this._fetchSeq) {
          this.loading = false;
        }
      }
    },
    async navigateToPath(targetPath) {
      const normalized = targetPath || '';
      if (normalized === this.currentPath && !this.loading) return;
      this.currentPath = normalized;
      this.searchKeyword = '';
      await this.reloadCurrentDirectory();
    },
    async reloadCurrentDirectory() {
      const seq = ++this._fetchSeq;
      this.loading = true;
      try {
        const repositoryId = this.$route.params.repositoryId;
        const snapshotId = this.$route.params.snapshotId;
        const nodes = await this.fetchTreeNodes(repositoryId, snapshotId, this.currentPath);
        if (this._destroyed || seq !== this._fetchSeq) return;
        if (nodes !== null) {
          this.files = nodes;
          this.tableRenderKey += 1;
        }
      } catch (e) {
        if (this._destroyed || seq !== this._fetchSeq) return;
        Message.error('加载文件失败: ' + e.message);
      } finally {
        if (!this._destroyed && seq === this._fetchSeq) {
          this.loading = false;
        }
      }
    },
    async loadSubdirectory(tree, treeNode, resolve) {
      const repositoryId = this.$route.params.repositoryId;
      const snapshotId = this.$route.params.snapshotId;
      const children = await this.fetchTreeNodes(repositoryId, snapshotId, tree.path);
      resolve(children || []);
    },
    restoreFile(file) {
      this.restoreItem = file;
      this.restoreForm.path = '';
      this.restoreForm.verify = false;
      this.restoreDialogVisible = true;
    },
    async downloadFile(file) {
      const repositoryId = this.$route.params.repositoryId;
      const snapshotId = this.$route.params.snapshotId;
      this.downloadingPath = file.path;
      try {
        await repositoryService.downloadSnapshotFile(repositoryId, snapshotId, file.path);
      } catch (e) {
        Message.error('下载失败: ' + (e.message || ''));
      } finally {
        this.downloadingPath = '';
      }
    },
    async performRestore() {
      const repositoryId = this.$route.params.repositoryId;
      const snapshotId = this.$route.params.snapshotId;
      if (!this.repository || !this.restoreItem || this.restoring) return;
      const targetPath = (this.restoreForm.path || '').trim();
      if (!targetPath) {
        Message.warning('请选择恢复目标目录');
        return;
      }
      this.restoring = true;
      try {
        let res;
        try {
          res = await repositoryService.restoreFile(repositoryId, snapshotId, this.restoreItem.path, targetPath, this.restoreForm.verify);
        } catch (e) {
          Message.error(e.response?.data?.message || e.message || '');
          return;
        }
        this.$store.dispatch('tasks/noteSubmitted', res.taskId);
        const task = await pollTask(res.taskId, { shouldStop: () => this._destroyed });
        if (this._destroyed) return;
        if (task.status === 'Succeeded') {
          Message.success('恢复成功');
          this.restoreDialogVisible = false;
        } else {
          Message.error('恢复失败: ' + (task.error || ''));
        }
      } catch (e) {
        if (e.message !== 'cancelled') Message.error('恢复失败: ' + (e.message || ''));
      } finally {
        this.restoring = false;
      }
    },
    formatFileSize(bytes) {
      if (!bytes) return '0 B';
      const k = 1024;
      const units = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.min(Math.floor(Math.log(bytes) / Math.log(k)), units.length - 1);
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
    },
    formatMtime(value) {
      if (!value) return '—';
      const d = new Date(value);
      if (isNaN(d.getTime())) return String(value);
      const pad = n => String(n).padStart(2, '0');
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }
  },
  mounted() { this.fetchFiles(); },
  beforeDestroy() {
    // 停止本页所有在途任务的轮询
    this._destroyed = true;
  }
};
</script>

<style scoped>
.snapshot-files-page {
  padding-bottom: var(--space-8);
}

/* 顶部精致信息卡 */
.files-hero {
  padding: 16px 20px;
  margin-bottom: var(--space-4);
  position: relative;
}

.files-hero::before {
  content: "";
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 2px;
  background: linear-gradient(90deg, var(--brand-500), var(--info-500) 40%, transparent 88%);
}

.files-hero__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--space-3);
}

.files-hero__identity {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.files-hero__snap-chip {
  padding: 3px 8px 3px 10px;
  color: var(--brand-300);
  border-color: var(--brand-200);
  background: var(--brand-50);
}

.files-hero__repo {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: var(--font-md);
  font-weight: 600;
  color: var(--color-text-primary);
}

.files-hero__repo i {
  color: var(--brand-400);
}

.files-hero__tools {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.files-hero__search {
  width: 240px;
}

/* 可点击路径面包屑 */
.path-breadcrumb {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 14px;
  padding-top: 12px;
  border-top: 1px solid var(--color-border-soft);
  flex-wrap: wrap;
}

.path-breadcrumb__label {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: var(--font-xs);
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.path-breadcrumb__track {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
  min-width: 0;
}

.path-crumb {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 3px 9px;
  border-radius: var(--radius-sm);
  border: 1px solid transparent;
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  font-size: 12px;
  cursor: pointer;
  transition: background-color var(--transition-fast), color var(--transition-fast), border-color var(--transition-fast);
}

.path-crumb:hover {
  background: var(--color-bg-hover);
  color: var(--color-text-primary);
  border-color: var(--brand-200);
}

.path-crumb--active {
  background: var(--brand-50);
  color: var(--brand-300);
  border-color: var(--brand-200);
  font-weight: 600;
}

.path-crumb__sep {
  font-size: 11px;
  color: var(--color-text-placeholder);
}

/* 表格容器 */
.files-surface {
  min-height: 220px;
}

.file-cell {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
  min-width: 0;
  max-width: 100%;
}

/* 树形表格中 Element UI 把展开箭头与列内容渲染为 .cell 内的兄弟节点，
   块级内容会把行内箭头挤到单独一行；让名称列 .cell 成为 flex 容器使两者同行居中 */
.snapshot-files-page >>> .el-table__body-wrapper .file-row td:first-child .cell {
  display: flex;
  align-items: center;
}

.file-cell__icon-wrap {
  width: 26px;
  height: 26px;
  border-radius: var(--radius-sm);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  flex-shrink: 0;
}

.file-cell__icon-wrap--folder {
  background: var(--warning-50);
  color: var(--warning-500);
}

.file-cell__icon-wrap--file {
  background: var(--info-50);
  color: var(--info-500);
}

.file-cell__name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-text-regular);
}

.file-cell__name--dir {
  font-weight: 600;
  color: var(--color-text-primary);
}

.file-cell__size {
  font-size: 12px;
  color: var(--color-text-regular);
}

.file-cell__perm {
  font-size: 11px;
  padding: 1px 7px;
}

.file-cell__mtime {
  font-size: 12px;
}

.file-actions {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
}

/* 空状态 */
.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 72px 20px;
}

.empty__icon {
  font-size: 38px;
  color: var(--color-text-placeholder);
  margin-bottom: 12px;
}

.empty__title {
  font-size: var(--font-lg);
  font-weight: 600;
  color: var(--color-text-primary);
}

.empty__desc {
  margin-top: 6px;
  font-size: var(--font-sm);
  color: var(--color-text-secondary);
}

.empty__action {
  margin-top: 14px;
}

/* 恢复弹窗 */
.restore-target-box {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  margin-bottom: 18px;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}

.restore-target-box__meta {
  min-width: 0;
  flex: 1;
}

.restore-target-box__name {
  font-size: var(--font-base);
  font-weight: 600;
  color: var(--color-text-primary);
}

.restore-target-box__path {
  margin-top: 3px;
  font-size: 12px;
  color: var(--color-text-secondary);
  word-break: break-all;
}

.form-tip {
  margin-top: 6px;
  font-size: var(--font-xs);
  color: var(--color-text-secondary);
}

.form-tip i {
  margin-right: 4px;
  color: var(--brand-400);
}

.restore-verify-item {
  margin-bottom: 0;
}

.restore-verify-box {
  padding: 10px 12px;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}

.restore-verify-box__desc {
  margin-top: 4px;
  padding-left: 24px;
  font-size: var(--font-xs);
  color: var(--color-text-secondary);
}

/* 紧凑行高 */
.file-row >>> td {
  padding-top: 7px !important;
  padding-bottom: 7px !important;
}

.file-cell-tight {
  line-height: 1.4;
}

@media (max-width: 768px) {
  .files-hero__search {
    width: 100%;
  }
  .files-hero__tools {
    width: 100%;
  }
}
</style>