<template>
  <div class="records-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div>
        <h2 class="page-header__title">备份记录</h2>
        <div class="page-header__subtitle">全面查看备份任务的执行历史、文件变动摘要与完整终端日志</div>
      </div>
      <div class="page-header__actions">
        <el-button round icon="el-icon-refresh" size="small" :loading="loading" @click="fetchRecords">刷新</el-button>
      </div>
    </div>

    <!-- 顶部紧凑统计条 -->
    <div class="rm-kpi-grid records-kpi-grid">
      <div class="rm-kpi-card rm-kpi-card--brand">
        <div class="rm-kpi-card__label">
          <span>当前结果总数</span>
          <i class="el-icon-tickets kpi-icon kpi-icon--brand"></i>
        </div>
        <div class="rm-kpi-card__value">{{ total }}</div>
        <div class="rm-kpi-card__meta">本页展示 {{ records.length }} 条记录</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--success">
        <div class="rm-kpi-card__label">
          <span>本页成功</span>
          <i class="el-icon-circle-check kpi-icon kpi-icon--success"></i>
        </div>
        <div class="rm-kpi-card__value">{{ pageSuccessCount }}</div>
        <div class="rm-kpi-card__meta">执行完成且无报错</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--danger">
        <div class="rm-kpi-card__label">
          <span>本页失败</span>
          <i class="el-icon-circle-close kpi-icon kpi-icon--danger"></i>
        </div>
        <div class="rm-kpi-card__value">{{ pageFailedCount }}</div>
        <div class="rm-kpi-card__meta">{{ pageFailedCount > 0 ? '点击「详情」查看错误输出' : '本页无失败记录' }}</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--info">
        <div class="rm-kpi-card__label">
          <span>运行中</span>
          <i class="el-icon-loading kpi-icon kpi-icon--info"></i>
        </div>
        <div class="rm-kpi-card__value">{{ pageRunningCount }}</div>
        <div class="rm-kpi-card__meta">{{ pageRunningCount > 0 ? '正在后台传输数据' : '本页无进行中任务' }}</div>
      </div>
    </div>

    <!-- 统一筛选工具条 -->
    <div class="rm-toolbar">
      <div class="rm-toolbar__group">
        <el-input
          v-model="filters.keyword"
          placeholder="按任务名或错误内容搜索..."
          prefix-icon="el-icon-search"
          clearable
          size="small"
          class="toolbar-search"
          @input="handleFilterChange"
        />

        <el-select
          v-model="filters.taskId"
          placeholder="全部备份任务"
          clearable
          size="small"
          filterable
          class="toolbar-select-wide"
          @change="handleFilterChange"
        >
          <el-option label="全部任务" :value="null"></el-option>
          <el-option v-for="task in tasks" :key="task.id" :label="task.name" :value="task.id"></el-option>
        </el-select>

        <el-select
          v-model="filters.status"
          placeholder="执行状态"
          clearable
          size="small"
          class="toolbar-select"
          @change="handleFilterChange"
        >
          <el-option label="全部状态" :value="null"></el-option>
          <el-option label="成功" value="Succeeded"></el-option>
          <el-option label="失败" value="Failed"></el-option>
          <el-option label="运行中" value="Running"></el-option>
          <el-option label="排队中" value="Pending"></el-option>
        </el-select>

        <el-button
          v-if="hasActiveFilter"
          size="small"
          plain
          icon="el-icon-refresh-left"
          @click="resetFilters"
        >重置</el-button>
      </div>
      <div class="rm-toolbar__group">
        <span class="toolbar-summary text-mono">共 {{ total }} 条匹配记录</span>
      </div>
    </div>

    <!-- 记录表格 -->
    <div class="rm-surface" v-loading="loading">
      <div v-if="!loading && records.length === 0" class="empty">
        <div class="empty__icon"><i class="el-icon-document"></i></div>
        <div class="empty__title">{{ hasActiveFilter ? '未找到符合条件的备份记录' : '暂无备份记录' }}</div>
        <div class="empty__desc">{{ hasActiveFilter ? '可尝试清空筛选条件或调整搜索关键字' : '在「备份任务」页面执行备份后，记录将展示在此处' }}</div>
        <el-button v-if="hasActiveFilter" size="small" type="primary" plain round class="mt-3" @click="resetFilters">清空筛选</el-button>
      </div>

      <div v-else>
        <el-table :data="records" stripe style="width: 100%">
          <!-- 任务信息 -->
          <el-table-column label="备份任务" min-width="210">
            <template #default="scope">
              <div class="task-info-cell">
                <div class="task-icon" :class="`task-icon--${getStatusTone(scope.row.status)}`">
                  <i :class="getStatusIcon(scope.row.status)"></i>
                </div>
                <div class="task-texts">
                  <div class="task-name" :title="getTaskName(scope.row)">
                    {{ getTaskName(scope.row) }}
                  </div>
                  <div class="task-sub" v-if="scope.row.task?.repositoryName || getRepoName(scope.row)">
                    <i class="el-icon-box"></i> {{ scope.row.task?.repositoryName || getRepoName(scope.row) }}
                  </div>
                </div>
              </div>
            </template>
          </el-table-column>

          <!-- 状态 -->
          <el-table-column label="状态" width="116" align="center">
            <template #default="scope">
              <span class="rm-status-pill" :class="getStatusPillClass(scope.row.status)">
                <span class="rm-status-pill__dot"></span>
                {{ getStatusText(scope.row.status) }}
              </span>
            </template>
          </el-table-column>

          <!-- 开始时间 -->
          <el-table-column label="开始时间" width="168" align="center">
            <template #default="scope">
              <span class="text-mono text-secondary" style="font-size: 12.5px;">{{ formatDate(scope.row.startTime) }}</span>
            </template>
          </el-table-column>

          <!-- 耗时 -->
          <el-table-column label="耗时" width="102" align="right">
            <template #default="scope">
              <span class="text-mono num-val">{{ calculateDuration(scope.row.startTime, scope.row.endTime) }}</span>
            </template>
          </el-table-column>

          <!-- 处理文件与变动摘要 -->
          <el-table-column label="文件与变动摘要" min-width="220">
            <template #default="scope">
              <div class="summary-cell">
                <div v-if="getRowSummary(scope.row)" class="summary-chips">
                  <span class="change-tag change-tag--new" title="新增文件">
                    +{{ getRowSummary(scope.row).files_new || 0 }}
                  </span>
                  <span class="change-tag change-tag--mod" title="修改文件">
                    ~{{ getRowSummary(scope.row).files_changed || 0 }}
                  </span>
                  <span class="change-tag change-tag--same" title="未变文件">
                    ={{ getRowSummary(scope.row).files_unmodified || 0 }}
                  </span>
                  <span
                    v-if="getSummaryBytes(getRowSummary(scope.row)) > 0"
                    class="change-tag change-tag--size"
                    title="摘要数据量"
                  >
                    {{ formatBytes(getSummaryBytes(getRowSummary(scope.row))) }}
                  </span>
                </div>
                <span v-else class="text-mono text-secondary" style="font-size: 12px;">
                  {{ scope.row.filesProcessed || 0 }} 文件
                </span>
              </div>
            </template>
          </el-table-column>

          <!-- 传输数据 -->
          <el-table-column label="传输数据" width="118" align="right">
            <template #default="scope">
              <span class="text-mono num-val">{{ formatBytes(scope.row.dataTransferred) }}</span>
            </template>
          </el-table-column>

          <!-- 操作 -->
          <el-table-column label="操作" width="150" align="right" fixed="right">
            <template #default="scope">
              <div class="action-btn-group">
                <el-button
                  size="mini"
                  type="primary"
                  plain
                  icon="el-icon-document"
                  @click="openDetailDrawer(scope.row)"
                >详情</el-button>
                <el-button
                  size="mini"
                  type="text"
                  class="text-danger"
                  icon="el-icon-delete"
                  @click="deleteRecord(scope.row)"
                >删除</el-button>
              </div>
            </template>
          </el-table-column>
        </el-table>

        <!-- 分页栏 -->
        <div class="pagination-wrapper">
          <el-pagination
            background
            @size-change="handleSizeChange"
            @current-change="handlePageChange"
            :current-page="page"
            :page-sizes="[10, 20, 50, 100]"
            :page-size="pageSize"
            layout="total, sizes, prev, pager, next, jumper"
            :total="total"
          />
        </div>
      </div>
    </div>

    <!-- 记录详情抽屉 -->
    <el-drawer
      :title="`${currentRecordTaskName || '备份记录'} · 执行详情`"
      :visible.sync="detailDrawerVisible"
      direction="rtl"
      size="700px"
      append-to-body
    >
      <div v-if="currentRecord" class="detail-drawer-body">
        <!-- 顶部状态摘要卡 -->
        <div class="drawer-summary-card">
          <div class="drawer-summary-card__head">
            <div class="drawer-summary-card__title-wrap">
              <div class="drawer-summary-card__title">{{ currentRecordTaskName }}</div>
              <div class="drawer-summary-card__sub" v-if="currentRecordRepoName">
                <i class="el-icon-box"></i> {{ currentRecordRepoName }}
              </div>
            </div>
            <span class="rm-status-pill" :class="getStatusPillClass(currentRecord.status)">
              <span class="rm-status-pill__dot"></span>
              {{ getStatusText(currentRecord.status) }}
            </span>
          </div>

          <div class="drawer-metrics-grid">
            <div class="drawer-metric">
              <span class="drawer-metric__label">开始时间</span>
              <span class="drawer-metric__val text-mono">{{ formatDate(currentRecord.startTime) }}</span>
            </div>
            <div class="drawer-metric">
              <span class="drawer-metric__label">结束时间</span>
              <span class="drawer-metric__val text-mono">{{ currentRecord.endTime ? formatDate(currentRecord.endTime) : '进行中' }}</span>
            </div>
            <div class="drawer-metric">
              <span class="drawer-metric__label">执行耗时</span>
              <span class="drawer-metric__val text-mono">{{ calculateDuration(currentRecord.startTime, currentRecord.endTime) }}</span>
            </div>
            <div class="drawer-metric">
              <span class="drawer-metric__label">处理文件</span>
              <span class="drawer-metric__val text-mono">{{ currentRecord.filesProcessed || 0 }}</span>
            </div>
            <div class="drawer-metric">
              <span class="drawer-metric__label">传输数据</span>
              <span class="drawer-metric__val text-mono">{{ formatBytes(currentRecord.dataTransferred) }}</span>
            </div>
            <div class="drawer-metric" v-if="currentRecordSummary">
              <span class="drawer-metric__label">文件变动</span>
              <span class="drawer-metric__val">
                <span class="change-tag change-tag--new">+{{ currentRecordSummary.files_new || 0 }}</span>
                <span class="change-tag change-tag--mod">~{{ currentRecordSummary.files_changed || 0 }}</span>
                <span class="change-tag change-tag--same">={{ currentRecordSummary.files_unmodified || 0 }}</span>
              </span>
            </div>
          </div>
        </div>

        <!-- 错误终端区（如有） -->
        <div v-if="currentRecord.error" class="rm-terminal terminal-block terminal-block--error">
          <div class="rm-terminal__header">
            <span class="terminal-title terminal-title--error">
              <i class="el-icon-warning"></i> Error Output
            </span>
            <el-button
              size="mini"
              type="text"
              icon="el-icon-document-copy"
              class="terminal-copy-btn"
              @click="copyText(currentRecord.error, '错误日志已复制')"
            >复制错误</el-button>
          </div>
          <div class="rm-terminal__body terminal-body--error">{{ currentRecord.error }}</div>
        </div>

        <!-- 标准输出终端区 -->
        <div class="rm-terminal terminal-block">
          <div class="rm-terminal__header">
            <span class="terminal-title">
              <i class="el-icon-tickets"></i> Standard Output
            </span>
            <el-button
              size="mini"
              type="text"
              icon="el-icon-document-copy"
              class="terminal-copy-btn"
              :disabled="!currentRecord.output && !currentRecord.error"
              @click="copyFullLog"
            >一键复制日志</el-button>
          </div>
          <div class="rm-terminal__body">{{ currentRecord.output || '（无标准日志输出）' }}</div>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui';
import { backupRecordService } from '@/services/backup-record.service';
import { backupService } from '@/services/backup.service';
import { repositoryService } from '@/services/repository.service';

function safeParseJson(json, fallback = null) {
  if (!json) return fallback;
  if (typeof json === 'object') return json;
  try {
    const parsed = JSON.parse(json);
    return parsed === null || parsed === undefined ? fallback : parsed;
  } catch (e) {
    return fallback;
  }
}

export default {
  name: 'BackupRecords',
  data() {
    return {
      records: [],
      tasks: [],
      repositories: [],
      loading: false,
      total: 0,
      page: 1,
      pageSize: 10,
      filters: {
        keyword: '',
        taskId: null,
        status: null
      },
      filterTimer: null,
      detailDrawerVisible: false,
      currentRecord: null
    };
  },
  computed: {
    hasActiveFilter() {
      return !!(this.filters.keyword || this.filters.taskId || this.filters.status);
    },
    taskMap() {
      const map = new Map();
      this.tasks.forEach(t => map.set(t.id, t));
      return map;
    },
    repoMap() {
      const map = new Map();
      this.repositories.forEach(r => map.set(r.id, r));
      return map;
    },
    pageSuccessCount() {
      return this.records.filter(r => r.status === 'Succeeded').length;
    },
    pageFailedCount() {
      return this.records.filter(r => r.status === 'Failed').length;
    },
    pageRunningCount() {
      return this.records.filter(r => r.status === 'Running').length;
    },
    currentRecordTaskName() {
      if (!this.currentRecord) return '';
      return this.getTaskName(this.currentRecord);
    },
    currentRecordRepoName() {
      if (!this.currentRecord) return '';
      return this.currentRecord.task?.repositoryName || this.getRepoName(this.currentRecord);
    },
    currentRecordSummary() {
      if (!this.currentRecord) return null;
      return this.getRowSummary(this.currentRecord);
    }
  },
  watch: {
    '$route.query.taskId'(val) {
      const parsed = this.parseTaskIdQuery(val);
      if (parsed !== this.filters.taskId) {
        this.clearFilterTimer();
        this.filters.taskId = parsed;
        this.page = 1;
        this.fetchRecords();
      }
    }
  },
  created() {
    this._fetchSeq = 0;
  },
  async mounted() {
    // 支持从路由 query 传参 taskId
    this.filters.taskId = this.parseTaskIdQuery(this.$route.query.taskId);
    await Promise.all([this.fetchMeta(), this.fetchRecords()]);
  },
  beforeDestroy() {
    this._destroyed = true;
    this.clearFilterTimer();
  },
  methods: {
    async fetchMeta() {
      try {
        const [tasks, repos] = await Promise.all([
          backupService.getTasks().catch(() => []),
          repositoryService.getRepositories().catch(() => [])
        ]);
        if (this._destroyed) return;
        this.tasks = tasks || [];
        this.repositories = repos || [];
      } catch (e) {
        console.error('获取元数据失败', e);
      }
    },
    parseTaskIdQuery(val) {
      if (val === undefined || val === null || val === '') return null;
      const num = Number(val);
      return Number.isFinite(num) && num > 0 ? num : null;
    },
    clearFilterTimer() {
      if (this.filterTimer) {
        clearTimeout(this.filterTimer);
        this.filterTimer = null;
      }
    },
    async fetchRecords() {
      const seq = ++this._fetchSeq;
      this.loading = true;
      try {
        const res = await backupRecordService.getRecordsPaged({
          page: this.page,
          pageSize: this.pageSize,
          taskId: this.filters.taskId || null,
          status: this.filters.status || null,
          keyword: this.filters.keyword ? this.filters.keyword.trim() : null
        });
        if (this._destroyed || seq !== this._fetchSeq) return;
        this.records = res.items || [];
        this.total = res.total || 0;
      } catch (e) {
        if (this._destroyed || seq !== this._fetchSeq) return;
        Message.error('加载备份记录失败: ' + (e.response?.data?.message || e.message));
      } finally {
        if (!this._destroyed && seq === this._fetchSeq) {
          this.loading = false;
        }
      }
    },
    handleFilterChange() {
      this.clearFilterTimer();
      this.filterTimer = setTimeout(() => {
        this.filterTimer = null;
        if (this._destroyed) return;
        this.page = 1;
        this.fetchRecords();
      }, 300);
    },
    resetFilters() {
      this.clearFilterTimer();
      this.filters = { keyword: '', taskId: null, status: null };
      this.page = 1;
      this.fetchRecords();
    },
    handlePageChange(newPage) {
      this.clearFilterTimer();
      this.page = newPage;
      this.fetchRecords();
    },
    handleSizeChange(newSize) {
      this.clearFilterTimer();
      this.pageSize = newSize;
      this.page = 1;
      this.fetchRecords();
    },
    getRowSummary(row) {
      if (!row) return null;
      if (row.summary && typeof row.summary === 'object') return row.summary;
      if (row.summaryJson) return safeParseJson(row.summaryJson, null);
      return null;
    },
    getSummaryBytes(summary) {
      if (!summary) return 0;
      return Number(summary.data_added || summary.total_bytes_processed || 0) || 0;
    },
    getTaskName(row) {
      if (row.task && row.task.name) return row.task.name;
      const taskId = row.backupTaskId || row.taskId;
      const found = this.taskMap.get(taskId);
      return found ? found.name : `任务 #${taskId}`;
    },
    getRepoName(row) {
      const taskId = row.backupTaskId || row.taskId;
      const task = this.taskMap.get(taskId);
      if (task && task.repositoryId) {
        const repo = this.repoMap.get(task.repositoryId);
        return repo ? repo.name : '';
      }
      return '';
    },
    getStatusTone(status) {
      switch (status) {
        case 'Succeeded': return 'success';
        case 'Failed': return 'danger';
        case 'Running': return 'warning';
        case 'Pending': return 'info';
        default: return 'neutral';
      }
    },
    getStatusPillClass(status) {
      switch (status) {
        case 'Succeeded': return 'rm-status-pill--success';
        case 'Failed': return 'rm-status-pill--danger';
        case 'Running': return 'rm-status-pill--running';
        case 'Pending': return 'rm-status-pill--info';
        default: return 'rm-status-pill--neutral';
      }
    },
    getStatusText(status) {
      switch (status) {
        case 'Succeeded': return '成功';
        case 'Failed': return '失败';
        case 'Running': return '运行中';
        case 'Pending': return '排队中';
        default: return status || '未知';
      }
    },
    getStatusIcon(status) {
      switch (status) {
        case 'Succeeded': return 'el-icon-check';
        case 'Failed': return 'el-icon-close';
        case 'Running': return 'el-icon-loading';
        default: return 'el-icon-tickets';
      }
    },
    formatDate(dateStr) {
      if (!dateStr) return '-';
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return dateStr;
      const pad = n => String(n).padStart(2, '0');
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
    },
    calculateDuration(start, end) {
      if (!start) return '-';
      const s = new Date(start).getTime();
      const e = end ? new Date(end).getTime() : Date.now();
      const diffSec = Math.max(0, Math.floor((e - s) / 1000));
      if (!end) return '进行中';
      if (diffSec < 60) return `${diffSec}s`;
      if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m${diffSec % 60}s`;
      return `${Math.floor(diffSec / 3600)}h${Math.floor((diffSec % 3600) / 60)}m`;
    },
    formatBytes(bytes) {
      if (!bytes || bytes === 0) return '0 B';
      const k = 1024;
      const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return (bytes / Math.pow(k, i)).toFixed(2) + ' ' + sizes[i];
    },
    openDetailDrawer(row) {
      this.currentRecord = row;
      this.detailDrawerVisible = true;
    },
    async copyFullLog() {
      if (!this.currentRecord) return;
      const parts = [];
      if (this.currentRecord.error) {
        parts.push('=== ERROR ===\n' + this.currentRecord.error);
      }
      if (this.currentRecord.output) {
        parts.push('=== OUTPUT ===\n' + this.currentRecord.output);
      }
      const text = parts.join('\n\n');
      if (!text) {
        Message.info('暂无可复制的日志内容');
        return;
      }
      await this.copyText(text, '完整日志已复制到剪贴板');
    },
    async copyText(text, successMsg) {
      if (!text) return;
      try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          await navigator.clipboard.writeText(text);
          Message.success(successMsg || '已复制到剪贴板');
          return;
        }
      } catch (e) {
        // 降级使用 textarea 复制
      }
      try {
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.setAttribute('readonly', '');
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        const ok = document.execCommand('copy');
        document.body.removeChild(textarea);
        if (ok) Message.success(successMsg || '已复制到剪贴板');
        else Message.info('请手动选中文本复制');
      } catch (err) {
        Message.info('请手动选中文本复制');
      }
    },
    async deleteRecord(row) {
      try {
        await MessageBox.confirm('确定要删除此条备份记录吗？该操作不可恢复。', '删除记录确认', {
          confirmButtonText: '确定删除',
          cancelButtonText: '取消',
          type: 'warning'
        });
        await backupRecordService.deleteRecord(row.id);
        Message.success('记录已删除');
        if (this.currentRecord && this.currentRecord.id === row.id) {
          this.detailDrawerVisible = false;
          this.currentRecord = null;
        }
        if (this.records.length === 1 && this.page > 1) {
          this.page -= 1;
        }
        await this.fetchRecords();
      } catch (e) {
        if (e !== 'cancel') {
          Message.error('删除失败: ' + (e.response?.data?.message || e.message));
        }
      }
    }
  }
};
</script>

<style scoped>
.records-page {
  padding-bottom: 32px;
}

.records-kpi-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.kpi-icon {
  font-size: 16px;
}
.kpi-icon--brand { color: var(--brand-400); }
.kpi-icon--success { color: var(--success-500); }
.kpi-icon--danger { color: var(--danger-500); }
.kpi-icon--info { color: var(--info-500); }

.toolbar-search {
  width: 260px;
  max-width: 100%;
}

.toolbar-select-wide {
  width: 210px;
}

.toolbar-select {
  width: 140px;
}

.toolbar-summary {
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 任务信息单元格 */
.task-info-cell {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.task-icon {
  width: 34px;
  height: 34px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 15px;
  flex-shrink: 0;
}

.task-icon--success { background: var(--success-50); color: var(--success-600); }
.task-icon--danger { background: var(--danger-50); color: var(--danger-600); }
.task-icon--warning { background: var(--warning-50); color: var(--warning-600); }
.task-icon--info { background: var(--info-50); color: var(--info-600); }
.task-icon--neutral { background: var(--gray-100); color: var(--color-text-secondary); }

.task-texts {
  min-width: 0;
  flex: 1;
}

.task-name {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 14px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.task-sub {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.task-sub i {
  margin-right: 3px;
}

.num-val {
  font-variant-numeric: tabular-nums;
  font-size: 13px;
  color: var(--color-text-primary);
}

/* 变动摘要胶囊 */
.summary-cell {
  display: flex;
  align-items: center;
}

.summary-chips {
  display: inline-flex;
  gap: 5px;
  align-items: center;
  flex-wrap: wrap;
}

.change-tag {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: var(--radius-sm);
  font-family: "JetBrains Mono", "SF Mono", Consolas, monospace;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.change-tag--new { background: var(--success-50); color: var(--success-600); }
.change-tag--mod { background: var(--warning-50); color: var(--warning-600); }
.change-tag--same { background: var(--gray-100); color: var(--color-text-secondary); }
.change-tag--size { background: var(--brand-50); color: var(--brand-400); }

/* 操作按钮列 */
.action-btn-group {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
  white-space: nowrap;
}

/* 分页包裹器 */
.pagination-wrapper {
  padding: 16px 20px;
  display: flex;
  justify-content: flex-end;
  border-top: 1px solid var(--color-border-soft);
}

/* 空状态 */
.empty {
  text-align: center;
  padding: 68px 20px;
}

.empty__icon {
  width: 56px;
  height: 56px;
  border-radius: var(--radius-xl);
  background: var(--brand-50);
  color: var(--brand-400);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 26px;
  margin-bottom: 14px;
}

.empty__title {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.empty__desc {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin-top: 6px;
}

/* 详情抽屉 */
.detail-drawer-body {
  padding: 20px 24px 28px;
  height: 100%;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.drawer-summary-card {
  padding: 16px 18px;
  border-radius: var(--radius-lg);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}

.drawer-summary-card__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 12px;
  margin-bottom: 12px;
  border-bottom: 1px dashed var(--color-border);
}

.drawer-summary-card__title {
  font-size: 16px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.drawer-summary-card__sub {
  margin-top: 4px;
  font-size: 12px;
  color: var(--color-text-secondary);
}

.drawer-metrics-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 16px;
}

.drawer-metric {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.drawer-metric__label {
  font-size: 11px;
  color: var(--color-text-secondary);
}

.drawer-metric__val {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  display: inline-flex;
  align-items: center;
  gap: 5px;
  flex-wrap: wrap;
}

.terminal-block--error {
  border-color: var(--danger-500);
}

.terminal-title {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
  color: var(--terminal-text);
}

.terminal-title--error {
  color: var(--danger-600);
}

.terminal-body--error {
  color: var(--danger-600);
}

.terminal-copy-btn {
  padding: 2px 6px;
}

@media (max-width: 992px) {
  .records-kpi-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .toolbar-search,
  .toolbar-select-wide,
  .toolbar-select {
    width: 100%;
  }
}
</style>
