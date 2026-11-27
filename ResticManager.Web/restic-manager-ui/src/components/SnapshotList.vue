<template>
  <div class="snapshot-list">
    <!-- 顶部工具栏与快速筛选 -->
    <div class="snapshot-toolbar">
      <div class="toolbar-left">
        <el-input
          v-model="searchKeyword"
          placeholder="搜索短 ID、主机名、标签或路径..."
          prefix-icon="el-icon-search"
          size="small"
          clearable
          class="toolbar-search"
        ></el-input>
        <div class="toolbar-stats">
          <span class="stat-pill">共 <strong>{{ snapshots.length }}</strong> 个快照</span>
          <span v-if="hasActiveFilter" class="stat-pill stat-pill--match">
            匹配 <strong>{{ filteredSnapshots.length }}</strong> 个
          </span>
        </div>
      </div>
      <div class="toolbar-right">
        <span class="toolbar-hint" v-if="selectedRows.length === 0 && snapshots.length > 0">
          <i class="el-icon-info"></i> 勾选 2 个快照可对比差异，勾选多个可批量删除
        </span>
      </div>
    </div>

    <!-- 选中态批处理操作条 -->
    <transition name="batch-bar-fade">
      <div v-if="selectedRows.length > 0" class="batch-action-bar">
        <div class="batch-action-bar__info">
          <span class="batch-action-bar__dot"></span>
          <span class="batch-action-bar__count">
            已选 <strong>{{ selectedRows.length }}</strong> 个快照
          </span>
          <span v-if="selectedRows.length === 2" class="batch-action-bar__pair">
            {{ formatShortTime(sortedSelectedPair[0].time) }} → {{ formatShortTime(sortedSelectedPair[1].time) }}
          </span>
          <span v-else class="batch-action-bar__tip">
            选择 2 个同路径快照即可对比差异
          </span>
        </div>
        <div class="batch-action-bar__actions">
          <el-button
            :type="selectedRows.length === 2 ? 'primary' : 'default'"
            :plain="selectedRows.length !== 2"
            size="mini"
            icon="el-icon-sort"
            :disabled="selectedRows.length !== 2"
            @click="compareSelectedRows"
          >对比差异</el-button>
          <el-button
            type="danger"
            plain
            size="mini"
            icon="el-icon-delete"
            @click="openBatchDelete"
          >批量删除</el-button>
          <el-button
            size="mini"
            type="text"
            class="batch-action-bar__clear"
            @click="clearTableSelection"
          >清空选择</el-button>
        </div>
      </div>
    </transition>

    <!-- 空状态 -->
    <div v-if="!loading && filteredSnapshots.length === 0" class="empty">
      <i :class="hasActiveFilter ? 'el-icon-search' : 'el-icon-camera'" class="empty__icon"></i>
      <div class="empty__title">{{ hasActiveFilter ? '未找到匹配的快照' : '该仓库暂无快照' }}</div>
      <div class="empty__desc">
        {{ hasActiveFilter ? '尝试更换关键词，或清空筛选条件查看全部快照' : '运行备份任务后，快照会出现在这里' }}
      </div>
      <el-button v-if="hasActiveFilter" size="small" plain class="mt-3" @click="searchKeyword = ''">
        清空筛选
      </el-button>
    </div>

    <!-- 快照表格 -->
    <div v-else v-loading="loading" class="snapshot-table-wrap">
      <el-table
        ref="snapshotTable"
        :data="paginatedSnapshots"
        row-key="id"
        size="small"
        stripe
        @selection-change="onSelectionChange"
      >
        <el-table-column type="selection" width="42" reserve-selection></el-table-column>
        <el-table-column label="时间" min-width="130">
          <template #default="scope">
            <span class="snapshot-time" :title="formatFullTime(scope.row.time)">
              {{ formatShortTime(scope.row.time) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="快照 ID" width="136">
          <template #default="scope">
            <div class="snapshot-id-cell">
              <span class="snapshot-id-pill text-mono" :title="scope.row.id || scope.row.short_id">
                {{ scope.row.short_id }}
              </span>
              <el-tooltip content="复制完整快照 ID" placement="top" :open-delay="300">
                <button
                  type="button"
                  class="copy-id-btn"
                  aria-label="复制完整快照 ID"
                  @click.stop="copySnapshotId(scope.row)"
                >
                  <i class="el-icon-document-copy"></i>
                </button>
              </el-tooltip>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="主机" width="115" show-overflow-tooltip>
          <template #default="scope">
            <span class="snapshot-host">{{ scope.row.hostname || '-' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="路径" min-width="160" show-overflow-tooltip>
          <template #default="scope">
            <span class="text-mono snapshot-path">{{ (scope.row.paths || []).join(', ') || '-' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="标签" min-width="145">
          <template #default="scope">
            <div v-if="scope.row.tags && scope.row.tags.length" class="tag-cell">
              <span v-for="t in scope.row.tags" :key="t" class="snapshot-tag-pill">{{ t }}</span>
            </div>
            <button
              v-else
              type="button"
              class="tag-ghost-trigger"
              @click="onEditTags(scope.row)"
            >
              <i class="el-icon-plus"></i> 添加标签
            </button>
          </template>
        </el-table-column>
        <el-table-column label="文件" width="76" align="right">
          <template #default="scope">
            <span class="tabular-num">{{ scope.row.summary?.total_files_processed || 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column label="大小" width="92" align="right">
          <template #default="scope">
            <span class="tabular-num">{{ formatFileSize(scope.row.summary?.total_bytes_processed || 0) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="耗时" width="78" align="right">
          <template #default="scope">
            <span class="tabular-num text-secondary">
              {{ formatDuration(scope.row.summary?.backup_end, scope.row.summary?.backup_start) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="278" fixed="right" align="right">
          <template #default="scope">
            <div class="row-actions">
              <el-button size="mini" type="text" icon="el-icon-folder-opened" class="row-action-btn" @click="onViewFiles(scope.row)">浏览文件</el-button>
              <el-button size="mini" type="text" icon="el-icon-refresh-left" class="row-action-btn" @click="onRestore(scope.row)">恢复</el-button>
              <el-button size="mini" type="text" icon="el-icon-price-tag" class="row-action-btn" @click="onEditTags(scope.row)">标签</el-button>
              <el-button size="mini" type="text" icon="el-icon-delete" class="row-action-btn row-action-btn--danger" @click="onDelete(scope.row)">删除</el-button>
            </div>
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
          <div class="form-tip"><i class="el-icon-info"></i> 校验会读取恢复后的文件与快照比对，耗时更长但更可靠</div>
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
          <span>确定要删除这个快照吗？删除后无法恢复</span>
        </template>
      </el-alert>
      <div class="delete-meta">
        <div><span class="text-secondary">快照 ID</span><span class="text-mono">{{ deleteItem?.short_id }}</span></div>
        <div><span class="text-secondary">主机名</span><span>{{ deleteItem?.hostname }}</span></div>
        <div><span class="text-secondary">创建时间</span><span>{{ deleteItem ? formatFullTime(deleteItem.summary?.backup_start || deleteItem.time) : '' }}</span></div>
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
          <span>确定要删除选中的 {{ batchDeleteItems.length }} 个快照吗？删除后无法恢复</span>
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
      <div class="form-tip"><i class="el-icon-info"></i> 保存将覆盖该快照的全部标签（restic tag --set）</div>
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
          <!-- 4 张 KPI 统计卡片 -->
          <div class="diff-kpi-grid">
            <div class="diff-kpi diff-kpi--add">
              <div class="diff-kpi__top">
                <span class="diff-kpi__label">新增</span>
                <span class="diff-code-badge diff-code-badge--add">+</span>
              </div>
              <div class="diff-kpi__num tabular-num">{{ diffStatsSummary.added }}</div>
            </div>
            <div class="diff-kpi diff-kpi--mod">
              <div class="diff-kpi__top">
                <span class="diff-kpi__label">修改</span>
                <span class="diff-code-badge diff-code-badge--mod">M</span>
              </div>
              <div class="diff-kpi__num tabular-num">{{ diffStatsSummary.modified }}</div>
            </div>
            <div class="diff-kpi diff-kpi--rem">
              <div class="diff-kpi__top">
                <span class="diff-kpi__label">删除</span>
                <span class="diff-code-badge diff-code-badge--rem">-</span>
              </div>
              <div class="diff-kpi__num tabular-num">{{ diffStatsSummary.removed }}</div>
            </div>
            <div class="diff-kpi diff-kpi--total">
              <div class="diff-kpi__top">
                <span class="diff-kpi__label">总变化</span>
                <span class="diff-code-badge diff-code-badge--total">Σ</span>
              </div>
              <div class="diff-kpi__num tabular-num">{{ diffStatsSummary.total }}</div>
            </div>
          </div>

          <!-- 紧凑筛选栏 -->
          <div class="diff-toolbar">
            <el-select v-model="diffTypeFilter" size="small" placeholder="全部类型" clearable class="diff-toolbar__select">
              <el-option label="新增 (+)" value="+"></el-option>
              <el-option label="修改 (M)" value="M"></el-option>
              <el-option label="删除 (-)" value="-"></el-option>
            </el-select>
            <el-input
              v-model="diffSearchKeyword"
              placeholder="搜索文件路径..."
              prefix-icon="el-icon-search"
              size="small"
              clearable
              class="diff-toolbar__search"
              @keyup.enter.native="diffCurrentPage = 1"
            ></el-input>
            <span class="diff-toolbar__count tabular-num">共 {{ filteredDiffChanges.length }} 项</span>
          </div>

          <el-table :data="paginatedDiffChanges" size="small" stripe>
            <el-table-column label="类型" width="120">
              <template #default="scope">
                <span class="diff-type-pill" :class="`diff-type-pill--${getModifierTone(scope.row.modifier)}`">
                  <span class="diff-type-pill__code text-mono">{{ getModifierCode(scope.row.modifier) }}</span>
                  <span class="diff-type-pill__text">{{ getModifierLabel(scope.row.modifier) }}</span>
                </span>
              </template>
            </el-table-column>
            <el-table-column label="文件路径">
              <template #default="scope">
                <span class="text-mono diff-path-text">{{ scope.row.path }}</span>
              </template>
            </el-table-column>
          </el-table>
          <el-pagination
            class="diff-pagination"
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
      diffRequestId: 0,
      comparedPair: [],
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
    diffTypeFilter() {
      this.diffCurrentPage = 1;
    },
    diffSearchKeyword() {
      this.diffCurrentPage = 1;
    },
    // 父组件刷新列表后（增删改/同步），清空勾选，避免已删除快照残留在批量删除选择中
    snapshots() {
      this.clearTableSelection();
      this.comparedPair = [];
      const maxPage = Math.max(1, Math.ceil(this.filteredSnapshots.length / this.pageSize));
      if (this.currentPage > maxPage) {
        this.currentPage = 1;
      }
    }
  },
  computed: {
    hasActiveFilter() {
      return Boolean((this.searchKeyword || '').trim());
    },
    filteredSnapshots() {
      const raw = (this.searchKeyword || '').trim();
      if (!raw) return this.snapshots;
      const q = raw.toLowerCase();
      return this.snapshots.filter(s => {
        const matchShortId = s.short_id && String(s.short_id).toLowerCase().includes(q);
        const matchFullId = s.id && String(s.id).toLowerCase().includes(q);
        const matchHost = s.hostname && String(s.hostname).toLowerCase().includes(q);
        const matchTags = (s.tags || []).some(t => String(t).toLowerCase().includes(q));
        const matchPaths = (s.paths || []).some(p => String(p).toLowerCase().includes(q));
        return matchShortId || matchFullId || matchHost || matchTags || matchPaths;
      });
    },
    paginatedSnapshots() {
      const s = (this.currentPage - 1) * this.pageSize;
      return this.filteredSnapshots.slice(s, s + this.pageSize);
    },
    sortedSelectedPair() {
      if (this.selectedRows.length !== 2) return [];
      return [...this.selectedRows].sort((a, b) => new Date(a.time || 0) - new Date(b.time || 0));
    },
    diffDrawerTitle() {
      if (this.comparedPair.length === 2) {
        return `${formatTime(this.comparedPair[0].time)} → ${formatTime(this.comparedPair[1].time)}`;
      }
      return '快照差异比较';
    },
    diffStatsSummary() {
      const stats = (this.diffResult && this.diffResult.statistics) || {};
      const changes = (this.diffResult && Array.isArray(this.diffResult.changes)) ? this.diffResult.changes : [];
      const added = typeof stats.added?.files === 'number'
        ? stats.added.files
        : changes.filter(c => c.modifier && c.modifier.includes('+')).length;
      const removed = typeof stats.removed?.files === 'number'
        ? stats.removed.files
        : changes.filter(c => c.modifier && c.modifier.includes('-')).length;
      const modified = changes.length > 0
        ? changes.filter(c => c.modifier && c.modifier.includes('M')).length
        : (typeof stats.changed_files === 'number' ? stats.changed_files : 0);
      const total = changes.length > 0
        ? changes.length
        : (added + removed + modified);
      return { added, modified, removed, total };
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
    compareSelectedRows() {
      if (this.selectedRows.length !== 2) return;
      const [first, second] = this.sortedSelectedPair;
      if (JSON.stringify(first.paths) !== JSON.stringify(second.paths)) {
        Message.warning('只能比较路径相同的快照');
        return;
      }
      this.comparedPair = [first, second];
      this.compareSnapshots(first, second);
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
    async copySnapshotId(snapshot) {
      const text = (snapshot && (snapshot.id || snapshot.short_id)) || '';
      if (!text) return;
      try {
        if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
          await navigator.clipboard.writeText(text);
        } else {
          this.fallbackCopyText(text);
        }
        Message.success('已复制完整快照 ID');
      } catch (e) {
        try {
          this.fallbackCopyText(text);
          Message.success('已复制完整快照 ID');
        } catch (err) {
          Message.error('复制失败，请手动复制');
        }
      }
    },
    fallbackCopyText(text) {
      const textarea = document.createElement('textarea');
      textarea.value = text;
      textarea.setAttribute('readonly', '');
      textarea.style.position = 'fixed';
      textarea.style.top = '-9999px';
      textarea.style.left = '-9999px';
      document.body.appendChild(textarea);
      textarea.select();
      const ok = document.execCommand('copy');
      document.body.removeChild(textarea);
      if (!ok) {
        throw new Error('execCommand copy failed');
      }
    },
    onViewFiles(snapshot) {
      const firstPath = Array.isArray(snapshot.paths) && snapshot.paths.length > 0 ? snapshot.paths[0] : '';
      this.$router.push({
        name: 'SnapshotFiles',
        params: {
          repositoryId: this.repositoryId,
          snapshotId: snapshot.id
        },
        query: {
          shortId: snapshot.short_id || '',
          path: firstPath
        }
      });
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
      const reqId = ++this.diffRequestId;
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
        if (this._destroyed || reqId !== this.diffRequestId) return;
        this.diffLoading = false;
        this.diffError = e.response?.data?.message || e.message || '';
        return;
      }
      if (this._destroyed || reqId !== this.diffRequestId) return;
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      try {
        const task = await pollTask(res.taskId, {
          shouldStop: () => this._destroyed || reqId !== this.diffRequestId
        });
        if (this._destroyed || reqId !== this.diffRequestId) return;
        if (task.status === 'Succeeded') {
          this.diffResult = task.resultJson ? JSON.parse(task.resultJson) : null;
        } else {
          this.diffError = '差异比较失败: ' + (task.error || '');
        }
      } catch (e) {
        if (this._destroyed || reqId !== this.diffRequestId) return;
        if (e.message !== 'cancelled') this.diffError = '差异比较失败: ' + (e.message || '');
      } finally {
        if (reqId === this.diffRequestId) {
          this.diffLoading = false;
        }
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
      if (!this.tagItem || this.tagSaving) return;
      this.tagSaving = true;
      let res;
      try {
        res = await repositoryService.setSnapshotTags(this.repositoryId, this.tagItem.id, this.tagList);
      } catch (e) {
        this.tagSaving = false;
        Message.error(e.response?.data?.message || e.message || '');
        return;
      }
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
      const k = 1024;
      const units = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
    },
    formatDuration(end, start) {
      if (!end || !start) return 'N/A';
      const ms = new Date(end) - new Date(start);
      if (ms < 0) return '无效';
      const s = Math.floor(ms / 1000);
      const m = Math.floor(s / 60);
      const h = Math.floor(m / 60);
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
    getModifierTone(m) {
      if (!m) return 'info';
      if (m.includes('+')) return 'add';
      if (m.includes('-')) return 'rem';
      if (m.includes('M')) return 'mod';
      return 'info';
    },
    getModifierCode(m) {
      if (!m) return '?';
      if (m.includes('+')) return '+';
      if (m.includes('-')) return '-';
      if (m.includes('M')) return 'M';
      return m.charAt(0);
    },
    getModifierLabel(m) {
      const map = { '+': '新增', '-': '删除', M: '修改', T: '类型', U: '元数据', '?': '损坏' };
      return (m || '').split('').map(c => map[c] || c).join('/');
    }
  }
};

function formatTime(t) {
  if (!t) return '-';
  return new Date(t).toLocaleString('zh-CN');
}
</script>

<style scoped>
.snapshot-list {
  color: var(--color-text-primary);
}

.tabular-num {
  font-variant-numeric: tabular-nums;
}

/* 顶部工具栏 */
.snapshot-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
  gap: 12px;
  flex-wrap: wrap;
}
.toolbar-left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  flex: 1;
  min-width: 240px;
}
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.toolbar-search {
  width: 300px;
  max-width: 100%;
}
.toolbar-stats {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}
.stat-pill {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  border-radius: var(--radius-full, 9999px);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  font-size: 12px;
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}
.stat-pill strong {
  color: var(--color-text-primary);
  font-weight: 600;
}
.stat-pill--match {
  background: var(--brand-50);
  border-color: var(--brand-200, var(--brand-500));
  color: var(--brand-400);
}
.stat-pill--match strong {
  color: var(--brand-400);
}
.toolbar-hint {
  font-size: 12px;
  color: var(--color-text-secondary);
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

/* 选中态批处理横幅 */
.batch-action-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  padding: 10px 14px;
  margin-bottom: 14px;
  border-radius: var(--radius-md, 10px);
  background: var(--brand-50);
  border: 1px solid var(--brand-500);
}
.batch-action-bar__info {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  font-size: 13px;
}
.batch-action-bar__dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--brand-400);
  box-shadow: 0 0 0 3px var(--brand-100);
  flex-shrink: 0;
}
.batch-action-bar__count {
  color: var(--color-text-primary);
}
.batch-action-bar__count strong {
  color: var(--brand-400);
  font-variant-numeric: tabular-nums;
}
.batch-action-bar__pair {
  font-size: 12px;
  color: var(--brand-400);
  padding: 2px 8px;
  border-radius: var(--radius-xs, 4px);
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  font-variant-numeric: tabular-nums;
}
.batch-action-bar__tip {
  font-size: 12px;
  color: var(--color-text-secondary);
}
.batch-action-bar__actions {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.batch-action-bar__clear {
  color: var(--color-text-secondary);
  padding-left: 6px;
  padding-right: 6px;
}
.batch-action-bar__clear:hover {
  color: var(--color-text-primary);
}
.batch-bar-fade-enter-active,
.batch-bar-fade-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}
.batch-bar-fade-enter,
.batch-bar-fade-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

/* 表格单元格美化 */
.snapshot-time {
  font-weight: 600;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}
.snapshot-id-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.snapshot-id-pill {
  display: inline-flex;
  align-items: center;
  font-size: 12px;
  color: var(--brand-400);
  padding: 2px 8px;
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-full, 9999px);
  line-height: 1.4;
}
.copy-id-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  padding: 0;
  border: 1px solid transparent;
  border-radius: var(--radius-xs, 4px);
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: color 150ms ease, background-color 150ms ease, border-color 150ms ease;
}
.copy-id-btn:hover {
  color: var(--brand-400);
  background: var(--brand-50);
  border-color: var(--color-border);
}
.snapshot-host {
  color: var(--color-text-primary);
}
.snapshot-path {
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 标签列 */
.tag-cell {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
}
.snapshot-tag-pill {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  font-size: 11px;
  line-height: 1.4;
  border-radius: var(--radius-full, 9999px);
  background: var(--brand-50);
  border: 1px solid var(--brand-200, var(--color-border));
  color: var(--brand-400);
}
.tag-ghost-trigger {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: 11px;
  line-height: 1.4;
  border-radius: var(--radius-full, 9999px);
  background: transparent;
  border: 1px dashed var(--color-border);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: color 150ms ease, border-color 150ms ease, background-color 150ms ease;
}
.tag-ghost-trigger:hover {
  color: var(--brand-400);
  border-color: var(--brand-500);
  background: var(--brand-50);
}

/* 操作列 */
.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  flex-wrap: nowrap;
}
.row-action-btn {
  margin-left: 0 !important;
  padding: 4px 6px;
  border-radius: var(--radius-xs, 4px);
}
.row-action-btn--danger {
  color: var(--danger-500) !important;
}
.row-action-btn--danger:hover {
  color: var(--danger-600) !important;
}

/* 标签编辑对话框 */
.tag-editor {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  padding: 12px;
  border-radius: var(--radius-md, 10px);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}
.tag-editor__tag { margin: 0; }
.tag-editor__input { width: 120px; }

.form-tip {
  margin-top: 8px;
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 批量删除对话框 */
.batch-delete-list {
  margin-top: 14px;
  max-height: 240px;
  overflow-y: auto;
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 10px);
  padding: 10px 14px;
}
.batch-delete-item {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 5px 0;
  font-size: 13px;
  border-bottom: 1px dashed var(--color-border);
}
.batch-delete-item:last-child {
  border-bottom: 0;
}

/* 空状态 */
.empty {
  text-align: center;
  padding: 56px 20px;
  background: var(--color-bg-card);
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-lg, 14px);
}
.empty--small {
  padding: 32px 20px;
}
.empty__icon {
  font-size: 32px;
  color: var(--color-text-secondary);
  margin-bottom: 10px;
  display: inline-block;
}
.empty__title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}
.empty__desc {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin-top: 6px;
}

/* 删除对话框元数据 */
.delete-meta {
  margin-top: 16px;
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 10px);
  padding: 12px 14px;
}
.delete-meta > div {
  display: flex;
  justify-content: space-between;
  padding: 6px 0;
  font-size: 13px;
  border-bottom: 1px dashed var(--color-border);
}
.delete-meta > div:last-child {
  border-bottom: 0;
}

/* 差异抽屉 */
.diff-drawer {
  padding: 16px 20px 24px;
}
.diff-loading {
  min-height: 220px;
}

/* 4 张小 KPI 卡片 */
.diff-kpi-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}
.diff-kpi {
  padding: 14px 16px;
  border-radius: var(--radius-md, 10px);
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  border-top-width: 3px;
}
.diff-kpi--add {
  border-top-color: var(--success-500);
  background: var(--success-50);
}
.diff-kpi--mod {
  border-top-color: var(--warning-500);
  background: var(--warning-50);
}
.diff-kpi--rem {
  border-top-color: var(--danger-500);
  background: var(--danger-50);
}
.diff-kpi--total {
  border-top-color: var(--brand-500);
  background: var(--brand-50);
}
.diff-kpi__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.diff-kpi__label {
  font-size: 12px;
  color: var(--color-text-secondary);
  font-weight: 500;
}
.diff-kpi__num {
  font-size: 24px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-top: 6px;
  line-height: 1.2;
}
.diff-code-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: var(--radius-xs, 4px);
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 12px;
  font-weight: 700;
}
.diff-code-badge--add {
  background: var(--success-500);
  color: var(--color-text-on-brand, #ffffff);
}
.diff-code-badge--mod {
  background: var(--warning-500);
  color: var(--color-text-on-brand, #ffffff);
}
.diff-code-badge--rem {
  background: var(--danger-500);
  color: var(--color-text-on-brand, #ffffff);
}
.diff-code-badge--total {
  background: var(--brand-500);
  color: var(--color-text-on-brand, #ffffff);
}

/* 差异筛选栏 */
.diff-toolbar {
  display: flex;
  gap: 10px;
  align-items: center;
  margin-bottom: 12px;
  padding: 10px 12px;
  border-radius: var(--radius-md, 10px);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
  flex-wrap: wrap;
}
.diff-toolbar__select {
  width: 132px;
}
.diff-toolbar__search {
  flex: 1;
  min-width: 180px;
  max-width: 340px;
}
.diff-toolbar__count {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 差异类型彩色等宽徽标 */
.diff-type-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 8px 2px 4px;
  border-radius: var(--radius-full, 9999px);
  font-size: 12px;
  font-weight: 500;
  border: 1px solid var(--color-border);
  background: var(--color-bg-soft);
  color: var(--color-text-primary);
}
.diff-type-pill__code {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  font-size: 11px;
  font-weight: 700;
}
.diff-type-pill--add {
  background: var(--success-50);
  border-color: var(--success-500);
  color: var(--success-600);
}
.diff-type-pill--add .diff-type-pill__code {
  background: var(--success-500);
  color: var(--color-text-on-brand, #ffffff);
}
.diff-type-pill--mod {
  background: var(--warning-50);
  border-color: var(--warning-500);
  color: var(--warning-600);
}
.diff-type-pill--mod .diff-type-pill__code {
  background: var(--warning-500);
  color: var(--color-text-on-brand, #ffffff);
}
.diff-type-pill--rem {
  background: var(--danger-50);
  border-color: var(--danger-500);
  color: var(--danger-600);
}
.diff-type-pill--rem .diff-type-pill__code {
  background: var(--danger-500);
  color: var(--color-text-on-brand, #ffffff);
}
.diff-type-pill--info {
  background: var(--brand-50);
  border-color: var(--brand-500);
  color: var(--brand-400);
}
.diff-type-pill--info .diff-type-pill__code {
  background: var(--brand-500);
  color: var(--color-text-on-brand, #ffffff);
}
.diff-path-text {
  font-size: 12px;
  color: var(--color-text-primary);
  word-break: break-all;
}

.diff-pagination,
.snapshot-pagination {
  margin-top: 16px;
  padding: 10px 0 4px;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 768px) {
  .diff-kpi-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .snapshot-pagination,
  .diff-pagination {
    justify-content: center;
  }
}
</style>