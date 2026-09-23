<template>
  <div class="records-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div>
        <h2 class="page-header__title">备份记录</h2>
        <div class="page-header__subtitle">全面查看备份任务的执行历史、文件变动与执行日志</div>
      </div>
      <div class="page-header__actions">
        <el-button round icon="el-icon-refresh" size="small" :loading="loading" @click="fetchRecords">刷新</el-button>
      </div>
    </div>

    <!-- 筛选卡片 -->
    <el-card class="filter-card mb-4" shadow="never">
      <div class="filter-row">
        <div class="filter-item filter-item--search">
          <el-input
            v-model="filters.keyword"
            placeholder="按任务名或错误内容搜索..."
            prefix-icon="el-icon-search"
            clearable
            size="small"
            @input="handleFilterChange"
          />
        </div>

        <div class="filter-item">
          <el-select
            v-model="filters.taskId"
            placeholder="全部备份任务"
            clearable
            size="small"
            filterable
            @change="handleFilterChange"
            style="width: 220px;"
          >
            <el-option label="全部任务" :value="null"></el-option>
            <el-option v-for="task in tasks" :key="task.id" :label="task.name" :value="task.id"></el-option>
          </el-select>
        </div>

        <div class="filter-item">
          <el-select
            v-model="filters.status"
            placeholder="执行状态"
            clearable
            size="small"
            @change="handleFilterChange"
            style="width: 140px;"
          >
            <el-option label="全部状态" :value="null"></el-option>
            <el-option label="成功" value="Succeeded"></el-option>
            <el-option label="失败" value="Failed"></el-option>
            <el-option label="运行中" value="Running"></el-option>
            <el-option label="排队中" value="Pending"></el-option>
          </el-select>
        </div>

        <div class="filter-item filter-item--reset" v-if="hasActiveFilter">
          <el-button size="small" plain type="info" icon="el-icon-refresh-left" @click="resetFilters">重置筛选</el-button>
        </div>
      </div>
    </el-card>

    <!-- 记录表格 -->
    <el-card shadow="never" class="table-card">
      <div v-if="!loading && records.length === 0" class="empty">
        <div class="empty__title">{{ hasActiveFilter ? '未找到符合条件的备份记录' : '暂无备份记录' }}</div>
        <div class="empty__desc">{{ hasActiveFilter ? '可尝试清空筛选条件或调整搜索关键字' : '在「备份任务」页面执行备份后，记录将展示在此处' }}</div>
        <el-button v-if="hasActiveFilter" size="small" type="primary" plain class="mt-3" @click="resetFilters">清空筛选</el-button>
      </div>

      <div v-else>
        <el-table
          :data="records"
          v-loading="loading"
          stripe
          style="width: 100%"
          class="custom-table"
        >
          <!-- 任务信息 -->
          <el-table-column label="备份任务" min-width="200">
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
                    <i class="el-icon-folder"></i> {{ scope.row.task?.repositoryName || getRepoName(scope.row) }}
                  </div>
                </div>
              </div>
            </template>
          </el-table-column>

          <!-- 状态 -->
          <el-table-column label="状态" width="120" align="center">
            <template #default="scope">
              <span class="rm-badge" :class="`rm-badge--${getStatusTone(scope.row.status)}`">
                <span class="rm-badge__dot"></span>
                {{ getStatusText(scope.row.status) }}
              </span>
            </template>
          </el-table-column>

          <!-- 开始时间 -->
          <el-table-column label="开始时间" width="170" align="center">
            <template #default="scope">
              <span class="text-mono" style="font-size: 13px;">{{ formatDate(scope.row.startTime) }}</span>
            </template>
          </el-table-column>

          <!-- 耗时 -->
          <el-table-column label="耗时" width="110" align="center">
            <template #default="scope">
              <span class="text-mono text-secondary">{{ calculateDuration(scope.row.startTime, scope.row.endTime) }}</span>
            </template>
          </el-table-column>

          <!-- 处理文件 -->
          <el-table-column label="处理文件" width="120" align="right">
            <template #default="scope">
              <span class="text-bold font-mono">{{ scope.row.filesProcessed || 0 }}</span>
            </template>
          </el-table-column>

          <!-- 变动摘要 -->
          <el-table-column label="变动概况" width="180" align="center">
            <template #default="scope">
              <div v-if="scope.row.summary" class="summary-chips">
                <span class="change-tag change-tag--new" title="新增文件">
                  +{{ scope.row.summary.files_new || 0 }}
                </span>
                <span class="change-tag change-tag--mod" title="修改文件">
                  ~{{ scope.row.summary.files_changed || 0 }}
                </span>
                <span class="change-tag change-tag--same" title="未变文件">
                  ={{ scope.row.summary.files_unmodified || 0 }}
                </span>
              </div>
              <span v-else class="text-muted">—</span>
            </template>
          </el-table-column>

          <!-- 传输数据 -->
          <el-table-column label="传输数据" width="130" align="right">
            <template #default="scope">
              <span class="text-mono text-secondary">{{ formatBytes(scope.row.dataTransferred) }}</span>
            </template>
          </el-table-column>

          <!-- 操作 -->
          <el-table-column label="操作" width="150" align="center" fixed="right">
            <template #default="scope">
              <div class="action-btn-group">
                <el-button
                  v-if="scope.row.output"
                  size="mini"
                  type="text"
                  icon="el-icon-tickets"
                  @click="openLogModal(scope.row)"
                >
                  日志
                </el-button>
                <el-button
                  v-if="scope.row.error"
                  size="mini"
                  type="text"
                  class="text-danger"
                  icon="el-icon-warning-outline"
                  @click="openErrorModal(scope.row)"
                >
                  错误
                </el-button>
                <el-button
                  size="mini"
                  type="text"
                  class="text-danger"
                  icon="el-icon-delete"
                  @click="deleteRecord(scope.row)"
                >
                  删除
                </el-button>
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
    </el-card>

    <!-- 日志查看弹窗 -->
    <el-dialog
      :title="`${currentRecordTaskName} · 执行日志`"
      :visible.sync="logDialogVisible"
      width="780px"
      append-to-body
      custom-class="log-dialog"
    >
      <div class="log-dialog__header-meta mb-2">
        <span class="text-secondary" style="font-size: 13px;">
          时间: {{ formatDate(currentRecord?.startTime) }}
        </span>
        <el-button size="mini" plain icon="el-icon-document-copy" @click="copyLog">复制日志</el-button>
      </div>
      <div class="log-terminal">
        <pre class="log-terminal__code"><code>{{ currentLogText || '（无日志输出）' }}</code></pre>
      </div>
      <template #footer>
        <el-button size="small" @click="logDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 错误详情弹窗 -->
    <el-dialog
      title="执行异常信息"
      :visible.sync="errorDialogVisible"
      width="600px"
      append-to-body
    >
      <div class="error-detail-box">
        <div class="error-detail-box__title"><i class="el-icon-error"></i> 备份过程中报告了以下错误：</div>
        <pre class="error-detail-box__content">{{ currentErrorText }}</pre>
      </div>
      <template #footer>
        <el-button size="small" @click="errorDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui';
import { backupRecordService } from '@/services/backup-record.service';
import { backupService } from '@/services/backup.service';
import { repositoryService } from '@/services/repository.service';

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
      logDialogVisible: false,
      errorDialogVisible: false,
      currentRecord: null,
      currentRecordTaskName: '',
      currentLogText: '',
      currentErrorText: ''
    };
  },
  computed: {
    hasActiveFilter() {
      return !!(this.filters.keyword || this.filters.taskId || this.filters.status);
    }
  },
  async mounted() {
    // 支持从路由 query 传参 taskId
    if (this.$route.query.taskId) {
      this.filters.taskId = Number(this.$route.query.taskId);
    }
    await Promise.all([this.fetchMeta(), this.fetchRecords()]);
  },
  methods: {
    async fetchMeta() {
      try {
        const [tasks, repos] = await Promise.all([
          backupService.getTasks().catch(() => []),
          repositoryService.getRepositories().catch(() => [])
        ]);
        this.tasks = tasks;
        this.repositories = repos;
      } catch (e) {
        console.error('获取元数据失败', e);
      }
    },
    async fetchRecords() {
      this.loading = true;
      try {
        const res = await backupRecordService.getRecordsPaged({
          page: this.page,
          pageSize: this.pageSize,
          taskId: this.filters.taskId || null,
          status: this.filters.status || null,
          keyword: this.filters.keyword ? this.filters.keyword.trim() : null
        });
        this.records = res.items || [];
        this.total = res.total || 0;
      } catch (e) {
        Message.error('加载备份记录失败: ' + (e.response?.data?.message || e.message));
      } finally {
        this.loading = false;
      }
    },
    handleFilterChange() {
      if (this.filterTimer) clearTimeout(this.filterTimer);
      this.filterTimer = setTimeout(() => {
        this.page = 1;
        this.fetchRecords();
      }, 300);
    },
    resetFilters() {
      this.filters = { keyword: '', taskId: null, status: null };
      this.page = 1;
      this.fetchRecords();
    },
    handlePageChange(newPage) {
      this.page = newPage;
      this.fetchRecords();
    },
    handleSizeChange(newSize) {
      this.pageSize = newSize;
      this.page = 1;
      this.fetchRecords();
    },
    getTaskName(row) {
      if (row.task && row.task.name) return row.task.name;
      const found = this.tasks.find(t => t.id === (row.backupTaskId || row.taskId));
      return found ? found.name : `任务 #${row.backupTaskId || row.taskId}`;
    },
    getRepoName(row) {
      const taskId = row.backupTaskId || row.taskId;
      const task = this.tasks.find(t => t.id === taskId);
      if (task && task.repositoryId) {
        const repo = this.repositories.find(r => r.id === task.repositoryId);
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
      if (diffSec < 60) return `${diffSec}秒`;
      if (diffSec < 3600) return `${Math.floor(diffSec / 60)}分${diffSec % 60}秒`;
      return `${Math.floor(diffSec / 3600)}时${Math.floor((diffSec % 3600) / 60)}分`;
    },
    formatBytes(bytes) {
      if (!bytes || bytes === 0) return '0 B';
      const k = 1024;
      const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return (bytes / Math.pow(k, i)).toFixed(2) + ' ' + sizes[i];
    },
    openLogModal(row) {
      this.currentRecord = row;
      this.currentRecordTaskName = this.getTaskName(row);
      this.currentLogText = row.output || '';
      this.logDialogVisible = true;
    },
    openErrorModal(row) {
      this.currentErrorText = row.error || '';
      this.errorDialogVisible = true;
    },
    async copyLog() {
      try {
        await navigator.clipboard.writeText(this.currentLogText);
        Message.success('日志已复制到剪贴板');
      } catch {
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

.filter-card {
  border: 1px solid var(--color-border);
  background: var(--color-bg-card);
}

.filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 14px;
}

.filter-item--search {
  width: 280px;
  max-width: 100%;
}

.table-card {
  border: 1px solid var(--color-border);
  background: var(--color-bg-card);
}

/* 任务信息单元格 */
.task-info-cell {
  display: flex;
  align-items: center;
  gap: 12px;
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
.task-icon--danger  { background: var(--danger-50); color: var(--danger-600); }
.task-icon--warning { background: var(--warning-50); color: var(--warning-600); }
.task-icon--info    { background: var(--info-50); color: var(--info-600); }
.task-icon--neutral { background: var(--gray-100); color: var(--gray-500); }

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
  margin-right: 2px;
}

/* 变动摘要胶囊 */
.summary-chips {
  display: inline-flex;
  gap: 6px;
  align-items: center;
}

.change-tag {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: var(--radius-sm);
  font-family: "SF Mono", Consolas, monospace;
  font-weight: 600;
}

.change-tag--new { background: var(--success-50); color: var(--success-600); }
.change-tag--mod { background: var(--warning-50); color: var(--warning-600); }
.change-tag--same { background: var(--gray-100); color: var(--gray-600); }

/* 操作按钮列 */
.action-btn-group {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  white-space: nowrap;
}

.action-btn-group .el-button {
  padding: 0;
  font-size: 13px;
}

/* 分页包裹器 */
.pagination-wrapper {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  border-top: 1px solid var(--color-border-soft);
}

/* 空状态 */
.empty {
  text-align: center;
  padding: 70px 20px;
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

/* 终端风格日志查看框 */
.log-dialog__header-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.log-terminal {
  background: #0f172a;
  border-radius: var(--radius-md);
  padding: 14px 16px;
  max-height: 480px;
  overflow-y: auto;
  border: 1px solid #1e293b;
}

.log-terminal__code {
  margin: 0;
  font-family: "SF Mono", Consolas, Menlo, Monaco, monospace;
  font-size: 12px;
  line-height: 1.6;
  color: #e2e8f0;
  white-space: pre-wrap;
  word-break: break-all;
}

/* 错误提示框 */
.error-detail-box {
  background: var(--danger-50);
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: var(--radius-md);
  padding: 14px 16px;
}

.error-detail-box__title {
  font-size: 13px;
  font-weight: 600;
  color: var(--danger-600);
  margin-bottom: 8px;
}

.error-detail-box__content {
  margin: 0;
  font-family: "SF Mono", Consolas, monospace;
  font-size: 12px;
  line-height: 1.5;
  color: var(--danger-600);
  white-space: pre-wrap;
  word-break: break-all;
}

.rm-badge__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
  display: inline-block;
}
</style>
