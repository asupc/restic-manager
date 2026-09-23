<template>
  <div class="snapshot-files-page">
    <div class="page-header">
      <div>
        <el-page-header @back="goBack" :content="`仓库 ${repository?.name || ''} · 快照 ${snapshotShortId} · 文件`">
        </el-page-header>
        <div v-if="currentPath" class="breadcrumb mt-2">
          <i class="el-icon-folder"></i>
          <span class="text-mono">{{ currentPath }}</span>
        </div>
      </div>
      <div class="page-header__actions">
        <el-input v-model="searchKeyword" placeholder="搜索文件..." prefix-icon="el-icon-search" size="small" clearable style="width:240px;"></el-input>
      </div>
    </div>

    <el-card shadow="never" v-loading="loading">
      <div v-if="filteredFiles.length === 0 && !loading" class="empty">
        <div class="empty__title">该目录下没有文件</div>
      </div>

      <el-table
        v-else
        :data="filteredFiles"
        row-key="path"
        :tree-props="{ children: 'children', hasChildren: 'hasChildren' }"
        lazy
        :load="loadSubdirectory"
        size="small"
        default-expand-all
        :row-class-name="'file-row'"
        :cell-class-name="'file-cell-tight'"
      >
        <el-table-column label="名称" min-width="320">
          <template slot-scope="scope">
            <div class="file-cell">
              <i :class="scope.row.type === 'dir' ? 'el-icon-folder file-cell__icon file-cell__icon--folder' : 'el-icon-document file-cell__icon file-cell__icon--file'"></i>
              <span class="file-cell__name">{{ scope.row.name }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="大小" width="120" align="right">
          <template slot-scope="scope">
            <span class="text-mono" v-if="scope.row.type === 'file'">{{ formatFileSize(scope.row.size) }}</span>
            <span v-else class="text-muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="路径" min-width="260">
          <template slot-scope="scope">
            <el-tooltip :content="scope.row.path" placement="top" :disabled="!scope.row.path">
              <span class="text-mono text-secondary file-cell__path">{{ scope.row.path }}</span>
            </el-tooltip>
          </template>
        </el-table-column>
        <!-- 树形表格下 fixed 列的克隆表行高与主表不一致会逐行错位，且本表无横向滚动，不做固定列 -->
        <el-table-column label="操作" width="200" align="center">
          <template slot-scope="scope">
            <el-button v-if="scope.row.type === 'file'" type="text" size="mini" icon="el-icon-download" :loading="downloadingPath === scope.row.path" @click="downloadFile(scope.row)">下载</el-button>
            <el-button type="text" size="mini" icon="el-icon-back" @click="restoreFile(scope.row)">恢复</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 恢复对话框 -->
    <el-dialog :visible.sync="restoreDialogVisible" title="恢复文件" width="500px" :close-on-click-modal="false" append-to-body>
      <el-alert type="info" :closable="false" show-icon class="mb-3">
        <template #title>
          <span>即将恢复 <b>{{ restoreItem?.name }}</b></span>
        </template>
      </el-alert>
      <el-form :model="restoreForm" label-position="top">
        <el-form-item label="恢复到目录">
          <folder-selector v-model="restoreForm.path" title="选择恢复路径" placeholder="选择恢复的目标目录"></folder-selector>
          <div class="form-tip"><i class="el-icon-info"></i> 文件将恢复到该目录下</div>
        </el-form-item>
        <el-form-item>
          <el-checkbox v-model="restoreForm.verify">恢复后校验文件内容 (--verify)</el-checkbox>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="restoreDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="restoring" @click="performRestore">恢复</el-button>
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
      restoring: false
    };
  },
  computed: {
    filteredFiles() {
      if (!this.searchKeyword) return this.files;
      const q = this.searchKeyword.toLowerCase();
      const filter = arr => arr.filter(f => f.name.toLowerCase().includes(q) || (f.children && f.children.length));
      // 简单筛选:不递归子节点
      return this.files.filter(f => f.name.toLowerCase().includes(q));
    }
  },
  methods: {
    goBack() { this.$router.go(-1); },
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
      this.loading = true;
      try {
        const repositoryId = this.$route.params.repositoryId;
        const snapshotId = this.$route.params.snapshotId;
        this.snapshotShortId = this.$route.query.shortId || snapshotId.substring(0, 8);
        this.currentPath = this.$route.query.path || '';
        this.repository = await repositoryService.getRepository(repositoryId);
        const nodes = await this.fetchTreeNodes(repositoryId, snapshotId, this.$route.query.path);
        if (nodes !== null) this.files = nodes;
      } catch (e) {
        Message.error('加载文件失败: ' + e.message);
      } finally {
        this.loading = false;
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
      if (!this.repository || !this.restoreItem) return;
      let res;
      try {
        res = await repositoryService.restoreFile(repositoryId, snapshotId, this.restoreItem.path, this.restoreForm.path, this.restoreForm.verify);
      } catch (e) {
        Message.error(e.response?.data?.message || e.message || '');
        return;
      }
      this.restoring = true;
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      try {
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
      const k = 1024, units = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
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
.snapshot-files-page { padding-bottom: 24px; }
.breadcrumb {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.file-cell { display: flex; align-items: center; gap: 8px; flex: 1; min-width: 0; max-width: 100%; }

/* 树形表格中 Element UI 把展开箭头与列内容渲染为 .cell 内的兄弟节点，
   块级内容会把行内箭头挤到单独一行；让名称列 .cell 成为 flex 容器使两者同行居中 */
.snapshot-files-page >>> .el-table__body-wrapper .file-row td:first-child .cell {
  display: flex;
  align-items: center;
}
.file-cell__icon { font-size: 16px; }
.file-cell__icon--folder { color: var(--brand-500); }
.file-cell__icon--file { color: var(--gray-400); }
.file-cell__name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.file-cell__path {
  font-size: 11px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 320px;
  display: inline-block;
  vertical-align: middle;
}

.empty { text-align: center; padding: 80px 20px; }
.empty__title { font-size: 16px; font-weight: 600; color: var(--color-text-primary); }

.form-tip { margin-top: 6px; font-size: 12px; color: var(--color-text-secondary); }
.form-tip i { margin-right: 4px; }

/* 紧凑行高 */
.file-row >>> td { padding-top: 6px !important; padding-bottom: 6px !important; }
.file-cell-tight { line-height: 1.4; }
</style>