<template>
  <div class="logs-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div>
        <h2 class="page-header__title">系统日志</h2>
        <div class="page-header__subtitle">实时排查应用运行日志：备份任务、调度器、仓库操作与异常堆栈</div>
      </div>
      <div class="page-header__actions">
        <span v-if="autoRefresh" class="rm-status-pill rm-status-pill--running">
          <span class="rm-status-pill__dot"></span>
          自动刷新中 (10s)
        </span>
        <span class="rm-status-pill rm-status-pill--neutral">
          共 <span class="text-mono">{{ total }}</span> 条
        </span>
      </div>
    </div>

    <!-- 统一筛选工具条 -->
    <div class="rm-toolbar logs-toolbar">
      <div class="rm-toolbar__group">
        <!-- 日志级别快捷胶囊筛选 -->
        <div class="level-pills" role="group" aria-label="日志级别筛选">
          <button
            v-for="item in levelOptions"
            :key="item.label"
            type="button"
            class="level-pill"
            :class="[
              'level-pill--' + item.tone,
              { 'is-active': filters.level === item.value }
            ]"
            @click="selectLevel(item.value)"
          >
            <span class="level-pill__dot"></span>
            <span>{{ item.label }}</span>
          </button>
        </div>
      </div>

      <div class="rm-toolbar__group logs-toolbar__controls">
        <el-input
          v-model="filters.keyword"
          placeholder="搜索消息、类别或异常..."
          prefix-icon="el-icon-search"
          clearable
          size="small"
          class="logs-toolbar__search"
          @input="handleFilterChange"
        />

        <el-date-picker
          v-model="dateRange"
          type="daterange"
          size="small"
          value-format="yyyy-MM-dd"
          range-separator="至"
          start-placeholder="开始日期"
          end-placeholder="结束日期"
          :clearable="true"
          class="logs-toolbar__date"
          @change="handleDateChange"
        />

        <div class="auto-refresh-toggle">
          <span class="auto-refresh-toggle__label">自动刷新</span>
          <el-switch v-model="autoRefresh" @change="handleAutoRefreshChange"></el-switch>
        </div>

        <el-button size="small" icon="el-icon-refresh" :loading="loading" @click="fetchLogs">刷新</el-button>
        <el-button
          v-if="hasActiveFilter"
          size="small"
          icon="el-icon-refresh-left"
          @click="resetFilters"
        >
          重置
        </el-button>
      </div>
    </div>

    <!-- 终端风格日志视图 -->
    <div class="rm-surface logs-surface">
      <div v-if="!loading && logs.length === 0" class="empty">
        <i class="el-icon-tickets empty__icon"></i>
        <div class="empty__title">{{ hasActiveFilter ? '未找到符合条件的日志' : '暂无系统日志' }}</div>
        <div class="empty__desc">{{ hasActiveFilter ? '可尝试清空筛选条件或调整搜索关键字' : '应用运行产生的日志将展示在此处（按天保留）' }}</div>
        <el-button v-if="hasActiveFilter" size="small" type="primary" plain class="empty__action" @click="resetFilters">清空筛选</el-button>
      </div>

      <div v-else>
        <el-table
          :data="logs"
          v-loading="loading"
          style="width: 100%"
          class="logs-table"
          :row-class-name="getRowClassName"
        >
          <!-- 展开异常堆栈 / 完整终端视图 -->
          <el-table-column type="expand" width="44">
            <template slot-scope="scope">
              <div class="log-expand-wrap">
                <div class="rm-terminal">
                  <div class="rm-terminal__header">
                    <div class="log-expand__meta">
                      <span class="log-level-badge" :class="'log-level-badge--' + getLevelTone(scope.row.level)">
                        {{ getLevelShort(scope.row.level) }}
                      </span>
                      <span class="text-mono">{{ formatTimestamp(scope.row.timestamp) }}</span>
                      <span class="log-expand__cat text-mono">{{ scope.row.category || '-' }}</span>
                    </div>
                    <el-button
                      size="mini"
                      icon="el-icon-document-copy"
                      @click="copyLogRow(scope.row)"
                    >
                      复制日志/堆栈
                    </el-button>
                  </div>
                  <div class="rm-terminal__body">{{ buildLogText(scope.row) }}</div>
                </div>
              </div>
            </template>
          </el-table-column>

          <!-- 时间 -->
          <el-table-column label="时间" width="176">
            <template slot-scope="scope">
              <span class="text-mono log-time">{{ formatTimestamp(scope.row.timestamp) }}</span>
            </template>
          </el-table-column>

          <!-- 等宽级别徽标 -->
          <el-table-column label="级别" width="84" align="center">
            <template slot-scope="scope">
              <span
                class="log-level-badge"
                :class="'log-level-badge--' + getLevelTone(scope.row.level)"
                :title="getLevelText(scope.row.level)"
              >
                {{ getLevelShort(scope.row.level) }}
              </span>
            </template>
          </el-table-column>

          <!-- 柔和 Category 标签 -->
          <el-table-column label="类别" width="230">
            <template slot-scope="scope">
              <span class="category-chip text-mono" :title="scope.row.category">
                {{ shortCategory(scope.row.category) }}
              </span>
            </template>
          </el-table-column>

          <!-- 消息 -->
          <el-table-column label="消息" min-width="300">
            <template slot-scope="scope">
              <div class="log-message-cell">
                <span :class="scope.row.exception ? 'text-danger' : 'log-message-text'">
                  {{ scope.row.message }}
                </span>
                <span v-if="scope.row.exception" class="rm-status-pill rm-status-pill--danger log-exc-tag">
                  含异常堆栈
                </span>
              </div>
            </template>
          </el-table-column>

          <!-- 操作 -->
          <el-table-column label="操作" width="120" align="center">
            <template slot-scope="scope">
              <el-button
                size="mini"
                type="text"
                icon="el-icon-tickets"
                @click="openDetail(scope.row)"
              >
                详情
              </el-button>
              <el-button
                size="mini"
                type="text"
                icon="el-icon-document-copy"
                @click="copyLogRow(scope.row)"
              >
                复制
              </el-button>
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
            :page-sizes="[20, 50, 100, 200]"
            :page-size="pageSize"
            layout="total, sizes, prev, pager, next, jumper"
            :total="total"
          />
        </div>
      </div>
    </div>

    <!-- 日志详情弹窗 -->
    <el-dialog
      title="日志详情"
      :visible.sync="detailVisible"
      width="800px"
      append-to-body
      custom-class="log-dialog"
    >
      <div class="rm-terminal">
        <div class="rm-terminal__header">
          <div class="log-expand__meta">
            <span
              v-if="currentLog"
              class="log-level-badge"
              :class="'log-level-badge--' + getLevelTone(currentLog.level)"
            >
              {{ getLevelShort(currentLog.level) }}
            </span>
            <span class="text-mono">{{ formatTimestamp(currentLog ? currentLog.timestamp : null) }}</span>
            <span class="log-expand__cat text-mono">{{ currentLog && currentLog.category ? currentLog.category : '-' }}</span>
          </div>
          <el-button size="mini" icon="el-icon-document-copy" @click="copyDetail">复制日志/堆栈</el-button>
        </div>
        <div class="rm-terminal__body">{{ detailText || '（无内容）' }}</div>
      </div>
      <template #footer>
        <el-button size="small" @click="detailVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { Message } from 'element-ui';
import { systemLogService } from '@/services/system-log.service';

export default {
  name: 'SystemLogs',
  data() {
    return {
      logs: [],
      loading: false,
      total: 0,
      page: 1,
      pageSize: 20,
      filters: {
        keyword: '',
        level: null
      },
      dateRange: null,
      filterTimer: null,
      autoRefresh: false,
      autoRefreshTimer: null,
      detailVisible: false,
      currentLog: null,
      detailText: '',
      levelOptions: [
        { label: '全部', value: null, tone: 'neutral' },
        { label: 'INFO', value: 'Information', tone: 'info' },
        { label: 'WARN', value: 'Warning', tone: 'warning' },
        { label: 'ERROR', value: 'Error', tone: 'danger' }
      ]
    };
  },
  computed: {
    hasActiveFilter() {
      return !!(this.filters.keyword || this.filters.level || this.dateRange);
    }
  },
  created() {
    this._fetchSeq = 0;
  },
  mounted() {
    this.fetchLogs();
  },
  beforeDestroy() {
    this._destroyed = true;
    this.clearFilterTimer();
    this.clearAutoRefreshTimer();
  },
  methods: {
    clearFilterTimer() {
      if (this.filterTimer) {
        clearTimeout(this.filterTimer);
        this.filterTimer = null;
      }
    },
    clearAutoRefreshTimer() {
      if (this.autoRefreshTimer) {
        clearInterval(this.autoRefreshTimer);
        this.autoRefreshTimer = null;
      }
    },
    handleAutoRefreshChange(enabled) {
      this.clearAutoRefreshTimer();
      if (enabled) {
        this.autoRefreshTimer = setInterval(() => {
          if (this._destroyed || this.loading) return;
          this.fetchLogs();
        }, 10000);
      }
    },
    selectLevel(level) {
      if (this.filters.level === level) return;
      this.clearFilterTimer();
      this.filters.level = level;
      this.page = 1;
      this.fetchLogs();
    },
    async fetchLogs() {
      const seq = ++this._fetchSeq;
      this.loading = true;
      try {
        const res = await systemLogService.getLogsPaged({
          page: this.page,
          pageSize: this.pageSize,
          level: this.filters.level || null,
          keyword: this.filters.keyword ? this.filters.keyword.trim() : null,
          startDate: (this.dateRange && this.dateRange[0]) || null,
          endDate: (this.dateRange && this.dateRange[1]) || null
        });
        if (this._destroyed || seq !== this._fetchSeq) return;
        this.logs = res.items || [];
        this.total = res.total || 0;
      } catch (e) {
        if (this._destroyed || seq !== this._fetchSeq) return;
        Message.error('加载日志失败: ' + (e.response?.data?.message || e.message));
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
        this.fetchLogs();
      }, 300);
    },
    handleDateChange() {
      this.clearFilterTimer();
      this.page = 1;
      this.fetchLogs();
    },
    resetFilters() {
      this.clearFilterTimer();
      this.filters = { keyword: '', level: null };
      this.dateRange = null;
      this.page = 1;
      this.fetchLogs();
    },
    handlePageChange(newPage) {
      this.clearFilterTimer();
      this.page = newPage;
      this.fetchLogs();
    },
    handleSizeChange(newSize) {
      this.clearFilterTimer();
      this.pageSize = newSize;
      this.page = 1;
      this.fetchLogs();
    },
    formatTimestamp(value) {
      if (!value) return '-';
      const d = new Date(value);
      if (isNaN(d.getTime())) return value;
      const pad = n => String(n).padStart(2, '0');
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
    },
    getLevelTone(level) {
      switch (level) {
        case 'Critical':
        case 'Fatal':
          return 'fatal';
        case 'Error':
          return 'danger';
        case 'Warning':
          return 'warning';
        case 'Information':
          return 'info';
        default:
          return 'neutral';
      }
    },
    getLevelShort(level) {
      switch (level) {
        case 'Trace': return 'TRC';
        case 'Debug': return 'DBG';
        case 'Information': return 'INF';
        case 'Warning': return 'WRN';
        case 'Error': return 'ERR';
        case 'Critical':
        case 'Fatal': return 'FTL';
        default: return 'LOG';
      }
    },
    getLevelText(level) {
      switch (level) {
        case 'Trace': return '跟踪';
        case 'Debug': return '调试';
        case 'Information': return '信息';
        case 'Warning': return '警告';
        case 'Error': return '错误';
        case 'Critical': return '严重';
        default: return level || '未知';
      }
    },
    getRowClassName({ row }) {
      if (row && (row.level === 'Error' || row.level === 'Critical')) return 'log-row--error';
      if (row && row.level === 'Warning') return 'log-row--warning';
      return '';
    },
    shortCategory(category) {
      if (!category) return '-';
      return category.replace('ResticManager.', '');
    },
    buildLogText(row) {
      if (!row) return '';
      let text = `[${this.formatTimestamp(row.timestamp)}] [${this.getLevelShort(row.level)}] ${row.category || '-'}\n${row.message || ''}`;
      if (row.exception) {
        text += '\n\n—— 异常堆栈 ——\n' + row.exception;
      }
      return text;
    },
    openDetail(row) {
      this.currentLog = row;
      this.detailText = this.buildLogText(row);
      this.detailVisible = true;
    },
    async copyLogRow(row) {
      await this.copyText(this.buildLogText(row));
    },
    async copyDetail() {
      await this.copyText(this.detailText);
    },
    async copyText(text) {
      try {
        await navigator.clipboard.writeText(text || '');
        Message.success('日志已复制到剪贴板');
      } catch (e) {
        Message.info('请手动选中文本复制');
      }
    }
  }
};
</script>

<style scoped>
.logs-page {
  padding-bottom: var(--space-8);
}

/* 顶部工具条 */
.logs-toolbar {
  align-items: center;
}

.logs-toolbar__controls {
  gap: var(--space-2);
}

.logs-toolbar__search {
  width: 240px;
  max-width: 100%;
}

.logs-toolbar__date {
  width: 246px;
}

/* 级别快捷胶囊筛选 */
.level-pills {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px;
  border-radius: var(--radius-full);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}

.level-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: var(--radius-full);
  border: 1px solid transparent;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--font-xs);
  font-weight: 600;
  cursor: pointer;
  transition: background-color var(--transition-fast), color var(--transition-fast), border-color var(--transition-fast);
}

.level-pill:hover {
  color: var(--color-text-primary);
  background: var(--color-bg-hover);
}

.level-pill__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
  opacity: 0.7;
}

.level-pill--neutral.is-active {
  background: var(--brand-50);
  color: var(--brand-300);
  border-color: var(--brand-200);
}

.level-pill--info.is-active {
  background: var(--info-50);
  color: var(--info-600);
  border-color: var(--info-500);
}

.level-pill--warning.is-active {
  background: var(--warning-50);
  color: var(--warning-600);
  border-color: var(--warning-500);
}

.level-pill--danger.is-active {
  background: var(--danger-50);
  color: var(--danger-600);
  border-color: var(--danger-500);
}

/* 自动刷新开关 */
.auto-refresh-toggle {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}

.auto-refresh-toggle__label {
  font-size: var(--font-xs);
  color: var(--color-text-secondary);
  white-space: nowrap;
}

/* 日志表格容器 */
.logs-surface {
  min-height: 240px;
}

.log-time {
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 醒目的等宽级别徽标 DBG/INF/WRN/ERR/FTL */
.log-level-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 44px;
  padding: 2px 7px;
  border-radius: var(--radius-xs);
  font-family: "JetBrains Mono", "SF Mono", Consolas, Menlo, Monaco, monospace;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.04em;
  line-height: 1.4;
  border: 1px solid transparent;
}

.log-level-badge--neutral {
  background: var(--gray-100);
  color: var(--color-text-secondary);
  border-color: var(--color-border);
}

.log-level-badge--info {
  background: var(--info-50);
  color: var(--info-600);
  border-color: var(--info-500);
}

.log-level-badge--warning {
  background: var(--warning-50);
  color: var(--warning-600);
  border-color: var(--warning-500);
}

.log-level-badge--danger {
  background: var(--danger-50);
  color: var(--danger-600);
  border-color: var(--danger-500);
}

.log-level-badge--fatal {
  background: var(--danger-500);
  color: var(--color-text-on-brand);
  border-color: var(--danger-500);
}

/* 柔和 Category 标签 */
.category-chip {
  display: inline-block;
  max-width: 100%;
  padding: 2px 8px;
  border-radius: var(--radius-xs);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border-soft);
  font-size: 11.5px;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: middle;
}

.log-message-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.log-message-text {
  color: var(--color-text-regular);
  word-break: break-word;
}

.log-exc-tag {
  padding: 1px 7px;
  font-size: 11px;
}

/* 展开行终端视图 */
.log-expand-wrap {
  padding: 10px 16px;
  background: var(--color-bg-soft);
}

.log-expand__meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
  min-width: 0;
}

.log-expand__cat {
  color: var(--color-text-secondary);
  word-break: break-all;
}

/* 分页 */
.pagination-wrapper {
  padding: 14px 16px;
  display: flex;
  justify-content: flex-end;
  border-top: 1px solid var(--color-border-soft);
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
  font-size: var(--font-sm);
  color: var(--color-text-secondary);
  margin-top: 6px;
}

.empty__action {
  margin-top: 14px;
}

@media (max-width: 768px) {
  .logs-toolbar__search,
  .logs-toolbar__date {
    width: 100%;
  }
}
</style>
