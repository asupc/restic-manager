<template>
  <div>
    <!-- 工具栏 -->
    <div class="snapshot-toolbar">
      <div class="toolbar-left">
        <el-input v-model="searchKeyword" placeholder="按主机名或路径搜索..." prefix-icon="el-icon-search" size="small" clearable class="toolbar-search"></el-input>
        <span class="text-secondary" style="font-size:12px;">共 {{ filteredSnapshots.length }} 个快照</span>
      </div>
      <div class="toolbar-right">
        <el-button v-if="!compareMode" type="danger" plain size="small" icon="el-icon-delete" @click="openBatchDelete" :disabled="selectedRows.length === 0">
          批量删除{{ selectedRows.length ? `(${selectedRows.length})` : '' }}
        </el-button>
        <el-button v-if="!compareMode" type="primary" plain size="small" icon="el-icon-compare" @click="enterCompareMode" :disabled="snapshots.length < 2">比较快照</el-button>
        <template v-else>
          <el-tag effect="plain" size="small">{{ selectedSnapshots.length }}/2 已选</el-tag>
          <el-button type="success" size="small" icon="el-icon-view" @click="showDiff" :disabled="selectedSnapshots.length !== 2">显示差异</el-button>
          <el-button size="small" @click="exitCompareMode">取消</el-button>
        </template>
      </div>
    </div>

    <!-- 选中提示 -->
    <el-alert v-if="compareMode && selectedSnapshots.length > 0" :title="'已选择: ' + selectedSnapshots.map(s => formatShortTime(s.time)).join('  →  ')" type="info" show-icon :closable="false" class="mb-3"></el-alert>

    <!-- 空状态 -->
    <div v-if="!loading && filteredSnapshots.length === 0" class="empty">
      <div class="empty__title">该仓库暂无快照</div>
      <div class="empty__desc">运行备份任务后,快照会出现在这里</div>
    </div>

    <!-- 快照表格 -->
    <div v-else v-loading="loading">
      <el-table
        ref="snapshotTable"
        :data="paginatedSnapshots"
        row-key="id"
        size="small"
        stripe
        @selection-change="onSelectionChange"
      >
        <el-table-column type="selection" width="42" reserve-selection></el-table-column>
        <el-table-column label="时间" min-width="125">
          <template #default="scope">
            <span class="text-bold">{{ formatShortTime(scope.row.time) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="快照 ID" width="105">
          <template #default="scope">
            <span class="text-mono snapshot-id">{{ scope.row.short_id }}</span>
          </template>
        </el-table-column>
        <el-table-column label="主机" width="110" show-overflow-tooltip>
          <template #default="scope">{{ scope.row.hostname }}</template>
        </el-table-column>
        <el-table-column label="路径" min-width="150" show-overflow-tooltip>
          <template #default="scope">
            <span class="text-mono">{{ (scope.row.paths || []).join(', ') }}</span>
          </template>
        </el-table-column>
        <el-table-column label="标签" min-width="130">
          <template #default="scope">
            <template v-if="scope.row.tags && scope.row.tags.length">
              <el-tag v-for="t in scope.row.tags" :key="t" size="mini" effect="plain" class="table-tag">{{ t }}</el-tag>
            </template>
            <span v-else class="text-secondary">-</span>
          </template>
        </el-table-column>
        <el-table-column label="文件" width="75" align="right">
          <template #default="scope">{{ scope.row.summary?.total_files_processed || 0 }}</template>
        </el-table-column>
        <el-table-column label="大小" width="90" align="right">
          <template #default="scope">{{ formatFileSize(scope.row.summary?.total_bytes_processed || 0) }}</template>
        </el-table-column>
        <el-table-column label="耗时" width="80" align="right">
          <template #default="scope">{{ formatDuration(scope.row.summary?.backup_end, scope.row.summary?.backup_start) }}</template>
        </el-table-column>
        <el-table-column v-if="compareMode" label="比较" width="55" align="center">
          <template #default="scope">
            <el-checkbox v-model="scope.row.checked" :disabled="!scope.row.checked && selectedSnapshots.length >= 2" @change="handleSnapshotSelect(scope.row)"></el-checkbox>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="350" fixed="right">
          <template #default="scope">
            <el-button size="mini" type="text" icon="el-icon-document" @click="onViewFiles(scope.row)">文件</el-button>
            <el-button size="mini" type="text" icon="el-icon-back" @click="onRestore(scope.row)">恢复</el-button>
            <el-button size="mini" type="text" icon="el-icon-price-tag" @click="onEditTags(scope.row)">标签</el-button>
            <el-button size="mini" type="text" class="text-danger" icon="el-icon-delete" @click="onDelete(scope.row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页栏 -->
      <div class="snapshot-pagination" v-if="filteredSnapshots.length > pageSize">
        <el-pagination
          background
          @size-change="(v) => { pageSize = v; currentPage = 1; }"
          @current-change="(v) => currentPage = v"
          :current-page="currentPage"
          :page-sizes="[10, 20, 50]"
          :page-size="pageSize"
          layout="total, sizes, prev, pager, next"
          :total="filteredSnapshots.length"
        />
      </div>
    </div>

    <!-- 恢复对话框 -->
    <el-dialog :visible.sync="restoreDialogVisible" title="恢复快照" width="500px" :close-on-click-modal="false" append-to-body>
      <el-form :model="restoreForm" label-position="top">
        <el-form-item label="恢复到目录">
          <folder-selector v-model="restoreForm.path" title="选择恢复路径" placeholder="选择或输入恢复目录"></folder-selector>
          <div class="form-tip"><i class="el-icon-info"></i> 快照内容将恢复到该目录下</div>
        </el-form-item>
        <el-form-item>
          <el-checkbox v-model="restoreForm.verify">恢复后校验文件内容 (--verify)</el-checkbox>
          <div class="form-tip"><i class="el-icon-info"></i> 校验会读取恢复后的文件与快照比对,耗时更长但更可靠</div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="restoreDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="performRestore">恢复</el-button>
      </template>
    </el-dialog>

    <!-- 删除对话框 -->
    <el-dialog :visible.sync="deleteDialogVisible" title="删除快照" width="480px" :close-on-click-modal="false" append-to-body>
      <el-alert type="warning" :closable="false" show-icon>
        <template #title>
          <span>确定要删除这个快照吗?删除后无法恢复</span>
        </template>
      </el-alert>
      <div class="delete-meta">
        <div><span class="text-secondary">快照 ID</span><span class="text-mono">{{ deleteItem?.short_id }}</span></div>
        <div><span class="text-secondary">主机名</span><span>{{ deleteItem?.hostname }}</span></div>
        <div><span class="text-secondary">创建时间</span><span>{{ deleteItem ? formatFullTime(deleteItem.summary?.backup_start) : '' }}</span></div>
      </div>
      <template #footer>
        <el-button @click="deleteDialogVisible = false">取消</el-button>
        <el-button type="danger" @click="performDelete">确认删除</el-button>
      </template>
    </el-dialog>

    <!-- 批量删除对话框 -->
    <el-dialog :visible.sync="batchDeleteDialogVisible" title="批量删除快照" width="520px" :close-on-click-modal="false" append-to-body>
      <el-alert type="warning" :closable="false" show-icon>
        <template #title>
          <span>确定要删除选中的 {{ batchDeleteItems.length }} 个快照吗?删除后无法恢复</span>
        </template>
      </el-alert>
      <div class="batch-delete-list">
        <div v-for="s in batchDeleteItems" :key="s.id" class="batch-delete-item">
          <span class="text-mono">{{ s.short_id }}</span>
          <span class="text-secondary">{{ formatShortTime(s.time) }}</span>
        </div>
      </div>
      <template #footer>
        <el-button @click="batchDeleteDialogVisible = false">取消</el-button>
        <el-button type="danger" @click="performBatchDelete">确认删除</el-button>
      </template>
    </el-dialog>

    <!-- 标签编辑对话框 -->
    <el-dialog :visible.sync="tagDialogVisible" title="编辑快照标签" width="480px" :close-on-click-modal="false" append-to-body>
      <div class="tag-editor">
        <el-tag
          v-for="t in tagList"
          :key="t"
          closable
          size="small"
          @close="removeTag(t)"
          class="tag-editor__tag"
        >{{ t }}</el-tag>
        <el-input
          v-if="tagInputVisible"
          ref="tagInput"
          v-model="tagInputValue"
          size="mini"
          class="tag-editor__input"
          @keyup.enter.native="confirmTagInput"
          @blur="confirmTagInput"
        ></el-input>
        <el-button v-else size="mini" icon="el-icon-plus" @click="showTagInput">添加标签</el-button>
      </div>
      <div class="form-tip"><i class="el-icon-info"></i> 保存将覆盖该快照的全部标签(restic tag --set)</div>
      <template #footer>
        <el-button @click="tagDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="tagSaving" @click="saveTags">保存</el-button>
      </template>
    </el-dialog>

    <!-- 差异抽屉 -->
    <el-drawer :visible.sync="diffDrawerVisible" :title="diffDrawerTitle" direction="rtl" size="70%" :with-header="true" append-to-body>
      <div class="diff-drawer">
        <el-alert v-if="diffError" type="error" :title="diffError" show-icon :closable="false" class="mb-3"></el-alert>
        <div v-if="diffLoading" v-loading="true" class="diff-loading"></div>
        <template v-else-if="diffResult && diffResult.statistics">
          <div class="diff-stats">
            <div class="diff-stat diff-stat--total">
              <div class="diff-stat__num">{{ diffResult.statistics.changed_files }}</div>
              <div class="diff-stat__label">变更文件</div>
            </div>
            <div class="diff-stat diff-stat--add">
              <div class="diff-stat__num">{{ diffResult.statistics.added?.files || 0 }}</div>
              <div class="diff-stat__label">新增</div>
            </div>
            <div class="diff-stat diff-stat--remove">
              <div class="diff-stat__num">{{ diffResult.statistics.removed?.files || 0 }}</div>
              <div class="diff-stat__label">删除</div>
            </div>
          </div>

          <div class="diff-toolbar">
            <el-select v-model="diffTypeFilter" size="small" placeholder="筛选变更类型" clearable style="width: 140px;">
              <el-option label="新增 (+)" value="+"></el-option>
              <el-option label="删除 (-)" value="-"></el-option>
              <el-option label="修改 (M)" value="M"></el-option>
            </el-select>
            <el-input v-model="diffSearchKeyword" placeholder="搜索文件路径..." prefix-icon="el-icon-search" size="small" clearable style="width: 280px;" @keyup.enter.native="diffCurrentPage = 1"></el-input>
            <span class="text-muted" style="font-size:12px;">{{ filteredDiffChanges.length }} 项</span>
          </div>

          <el-table :data="paginatedDiffChanges" size="small" stripe>
            <el-table-column label="类型" width="100">
              <template #default="scope">
                <span class="rm-badge" :class="`rm-badge--${getModifierType(scope.row.modifier)}`">
                  {{ getModifierLabel(scope.row.modifier) }}
                </span>
              </template>
            </el-table-column>
            <el-table-column label="文件路径">
              <template #default="scope">
                <span class="text-mono" style="font-size:12px;">{{ scope.row.path }}</span>
              </template>
            </el-table-column>
          </el-table>
          <el-pagination
            class="mt-3"
            background
            layout="total, sizes, prev, pager, next"
            :total="filteredDiffChanges.length"
            :page-size="diffPageSize"
            :current-page="diffCurrentPage"
            :page-sizes="[10, 20, 50, 100]"
            @current-change="diffCurrentPage = $event"
            @size-change="(v) => { diffPageSize = v; diffCurrentPage = 1; }"
          ></el-pagination>
        </template>
        <div v-else class="empty empty--small">
          <div class="empty__title">暂无差异信息</div>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<script>
import { Message } from 'element-ui';
import FolderSelector from '@/components/FolderSelector.vue';
import { repositoryService } from '@/services/repository.service';
import { pollTask } from '@/utils/task-poller';

export default {
  name: 'SnapshotList',
  components: { FolderSelector },
  props: {
    snapshots: { type: Array, default: () => [] },
    showDetails: { type: Boolean, default: true },
    repositoryId: { type: [String, Number], required: true },
    loading: { type: Boolean, default: false }
  },
  data() {
    return {
      searchKeyword: '',
      compareMode: false,
      restoreDialogVisible: false,
      deleteDialogVisible: false,
      batchDeleteDialogVisible: false,
      batchDeleteItems: [],
      selectedRows: [],
      diffDrawerVisible: false,
      restoreItem: null,
      deleteItem: null,
      diffResult: null,
      diffError: '',
      diffLoading: false,
      selectedSnapshots: [],
      restoreForm: { path: '', verify: false },
      diffCurrentPage: 1,
      diffPageSize: 20,
      diffSearchKeyword: '',
      diffTypeFilter: '',
      tagDialogVisible: false,
      tagItem: null,
      tagList: [],
      tagInputVisible: false,
      tagInputValue: '',
      tagSaving: false,
      currentPage: 1,
      pageSize: 10
    };
  },
  watch: {
    searchKeyword() {
      this.currentPage = 1;
    },
    // 父组件刷新列表后（增删改/同步），清空勾选，避免已删除快照残留在批量删除选择中
    snapshots() {
      this.clearTableSelection();
    }
  },
  computed: {
    filteredSnapshots() {
      if (!this.searchKeyword) return this.snapshots;
      const q = this.searchKeyword.toLowerCase();
      return this.snapshots.filter(s =>
        (s.hostname && s.hostname.toLowerCase().includes(q)) ||
        (s.paths || []).some(p => p.toLowerCase().includes(q))
      );
    },
    paginatedSnapshots() {
      const s = (this.currentPage - 1) * this.pageSize;
      return this.filteredSnapshots.slice(s, s + this.pageSize);
    },
    diffDrawerTitle() {
      if (this.selectedSnapshots.length === 2) {
        return `${formatTime(this.selectedSnapshots[0].time)} → ${formatTime(this.selectedSnapshots[1].time)}`;
      }
      return '快照差异比较';
    },
    filteredDiffChanges() {
      if (!this.diffResult || !this.diffResult.changes) return [];
      let arr = this.diffResult.changes;
      if (this.diffTypeFilter) arr = arr.filter(c => c.modifier && c.modifier.includes(this.diffTypeFilter));
      if (this.diffSearchKeyword) {
        const q = this.diffSearchKeyword.toLowerCase();
        arr = arr.filter(c => c.path && c.path.toLowerCase().includes(q));
      }
      return arr;
    },
    paginatedDiffChanges() {
      const s = (this.diffCurrentPage - 1) * this.diffPageSize;
      return this.filteredDiffChanges.slice(s, s + this.diffPageSize);
    }
  },
  beforeDestroy() {
    // 停止本组件在途任务的轮询
    this._destroyed = true;
    this.snapshots.forEach(s => { if ('checked' in s) this.$delete(s, 'checked'); });
  },
  methods: {
    // 表格勾选变化（reserve-selection 下跨页保留）
    onSelectionChange(rows) {
      this.selectedRows = rows;
    },
    clearTableSelection() {
      this.selectedRows = [];
      if (this.$refs.snapshotTable) {
        this.$refs.snapshotTable.clearSelection();
      }
    },
    openBatchDelete() {
      if (this.selectedRows.length === 0) return;
      this.batchDeleteItems = [...this.selectedRows];
      this.batchDeleteDialogVisible = true;
    },
    performBatchDelete() {
      this.$emit('delete-snapshots', { repositoryId: this.repositoryId, snapshots: this.batchDeleteItems });
      this.batchDeleteDialogVisible = false;
      this.clearTableSelection();
    },
    enterCompareMode() {
      this.compareMode = true;
      this.snapshots.forEach(s => this.$set(s, 'checked', false));
      this.selectedSnapshots = [];
    },
    exitCompareMode() {
      this.compareMode = false;
      this.selectedSnapshots = [];
    },
    handleSnapshotSelect(snapshot) {
      if (snapshot.checked) {
        if (this.selectedSnapshots.length < 2) {
          if (this.selectedSnapshots.length > 0) {
            const first = this.selectedSnapshots[0];
            if (JSON.stringify(first.paths) !== JSON.stringify(snapshot.paths)) {
              snapshot.checked = false;
              Message.warning('只能比较路径相同的快照');
              return;
            }
          }
          this.selectedSnapshots.push(snapshot);
        } else {
          snapshot.checked = false;
          Message.warning('最多选择 2 个快照');
        }
      } else {
        const idx = this.selectedSnapshots.findIndex(s => s.id === snapshot.id);
        if (idx > -1) this.selectedSnapshots.splice(idx, 1);
      }
    },
    showDiff() {
      if (this.selectedSnapshots.length !== 2) return;
      this.compareSnapshots(this.selectedSnapshots[0], this.selectedSnapshots[1]);
    },
    onViewFiles(snapshot) {
      this.$router.push(`/repositories/${this.repositoryId}/snapshots/${snapshot.id}/files?shortId=${snapshot.short_id}&path=${snapshot.paths[0]}`);
    },
    onRestore(snapshot) {
      this.restoreItem = snapshot;
      this.restoreForm.path = '';
      this.restoreForm.verify = false;
      this.restoreDialogVisible = true;
    },
    onDelete(snapshot) {
      this.deleteItem = snapshot;
      this.deleteDialogVisible = true;
    },
    async compareSnapshots(s1, s2) {
      this.diffDrawerVisible = true;
      this.diffLoading = true;
      this.diffResult = null;
      this.diffError = '';
      this.diffCurrentPage = 1;
      this.diffSearchKeyword = '';
      this.diffTypeFilter = '';
      let res;
      try {
        res = await repositoryService.diffSnapshots(this.repositoryId, s1.id, s2.id);
      } catch (e) {
        this.diffLoading = false;
        this.diffError = e.response?.data?.message || e.message || '';
        return;
      }
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      try {
        const task = await pollTask(res.taskId, { shouldStop: () => this._destroyed });
        if (this._destroyed) return;
        if (task.status === 'Succeeded') {
          this.diffResult = task.resultJson ? JSON.parse(task.resultJson) : null;
        } else {
          this.diffError = '差异比较失败: ' + (task.error || '');
        }
      } catch (e) {
        if (e.message !== 'cancelled') this.diffError = '差异比较失败: ' + (e.message || '');
      } finally {
        this.diffLoading = false;
      }
    },
    performRestore() {
      this.$emit('restore-snapshot', { repositoryId: this.repositoryId, snapshot: this.restoreItem, targetPath: this.restoreForm.path, verify: this.restoreForm.verify });
      this.restoreDialogVisible = false;
    },
    performDelete() {
      this.$emit('delete-snapshot', { repositoryId: this.repositoryId, snapshot: this.deleteItem });
      this.deleteDialogVisible = false;
    },
    onEditTags(snapshot) {
      this.tagItem = snapshot;
      this.tagList = [...(snapshot.tags || [])];
      this.tagInputVisible = false;
      this.tagInputValue = '';
      this.tagDialogVisible = true;
    },
    showTagInput() {
      this.tagInputVisible = true;
      this.$nextTick(() => {
        const input = this.$refs.tagInput;
        if (input) input.focus();
      });
    },
    confirmTagInput() {
      const val = (this.tagInputValue || '').trim();
      if (val && !this.tagList.includes(val)) {
        this.tagList.push(val);
      }
      this.tagInputVisible = false;
      this.tagInputValue = '';
    },
    removeTag(tag) {
      this.tagList = this.tagList.filter(t => t !== tag);
    },
    async saveTags() {
      if (!this.tagItem) return;
      let res;
      try {
        res = await repositoryService.setSnapshotTags(this.repositoryId, this.tagItem.id, this.tagList);
      } catch (e) {
        Message.error(e.response?.data?.message || e.message || '');
        return;
      }
      this.tagSaving = true;
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      try {
        const task = await pollTask(res.taskId, { shouldStop: () => this._destroyed });
        if (this._destroyed) return;
        if (task.status === 'Succeeded') {
          Message.success('标签已更新');
          this.tagDialogVisible = false;
          this.$emit('tags-updated');
        } else {
          Message.error('标签更新失败: ' + (task.error || ''));
        }
      } catch (e) {
        if (e.message !== 'cancelled') Message.error('标签更新失败: ' + (e.message || ''));
      } finally {
        this.tagSaving = false;
      }
    },
    formatFileSize(bytes) {
      if (!bytes) return '0 B';
      const k = 1024, units = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
    },
    formatDuration(end, start) {
      if (!end || !start) return 'N/A';
      const ms = new Date(end) - new Date(start);
      if (ms < 0) return '无效';
      const s = Math.floor(ms / 1000), m = Math.floor(s / 60), h = Math.floor(m / 60);
      if (h) return `${h}h${m % 60}m`;
      if (m) return `${m}m${s % 60}s`;
      return `${s}s`;
    },
    formatShortTime(t) {
      if (!t) return '-';
      return new Date(t).toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    },
    formatFullTime(t) {
      if (!t) return '-';
      return new Date(t).toLocaleString('zh-CN', { hour12: false });
    },
    getModifierType(m) {
      if (!m) return 'info';
      if (m.includes('+')) return 'success';
      if (m.includes('-')) return 'danger';
      if (m.includes('M')) return 'warning';
      return 'info';
    },
    getModifierLabel(m) {
      const map = { '+': '新增', '-': '删除', M: '修改', T: '类型', U: '元数据', '?': '损坏' };
      return (m || '').split('').map(c => map[c] || c).join(',');
    }
  }
};

function formatTime(t) {
  if (!t) return '-';
  return new Date(t).toLocaleString('zh-CN');
}
</script>

<style scoped>
.snapshot-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  gap: 12px;
  flex-wrap: wrap;
}
.toolbar-left { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.toolbar-right { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.toolbar-search { width: 280px; max-width: 100%; }

.snapshot-id {
  font-size: 11px;
  color: var(--color-text-secondary);
  padding: 1px 6px;
  background: var(--color-bg-soft);
  border-radius: 4px;
}
.table-tag { margin-right: 4px; margin-bottom: 2px; }

.tag-editor { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; }
.tag-editor__tag { margin: 0; }
.tag-editor__input { width: 120px; }

/* 批量删除对话框 */
.batch-delete-list {
  margin-top: 14px;
  max-height: 240px;
  overflow-y: auto;
  background: var(--color-bg-soft);
  border-radius: var(--radius-md);
  padding: 10px 14px;
}
.batch-delete-item {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 4px 0;
  font-size: 13px;
}

/* 空状态 */
.empty { text-align: center; padding: 60px 20px; }
.empty--small { padding: 30px 20px; }
.empty__title { font-size: 16px; font-weight: 600; color: var(--color-text-primary); }
.empty__desc { font-size: 13px; color: var(--color-text-secondary); margin-top: 4px; }

/* 删除对话框元数据 */
.delete-meta {
  margin-top: 16px;
  background: var(--color-bg-soft);
  border-radius: var(--radius-md);
  padding: 14px;
}
.delete-meta > div {
  display: flex;
  justify-content: space-between;
  padding: 6px 0;
  font-size: 13px;
}

/* 差异抽屉 */
.diff-drawer { padding: 16px; }
.diff-loading { min-height: 200px; }

.diff-stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin-bottom: 16px;
}
.diff-stat {
  text-align: center;
  padding: 16px;
  border-radius: var(--radius-lg);
  background: var(--color-bg-soft);
}
.diff-stat--total { background: var(--brand-50); }
.diff-stat--add { background: var(--success-50); }
.diff-stat--remove { background: var(--danger-50); }
.diff-stat__num {
  font-size: 26px;
  font-weight: 700;
  color: var(--color-text-primary);
}
.diff-stat__label {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 4px;
}

.diff-toolbar {
  display: flex;
  gap: 10px;
  align-items: center;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.snapshot-pagination {
  margin-top: 18px;
  padding: 14px 0 6px;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 768px) {
  .snapshot-pagination { justify-content: center; }
}
</style>