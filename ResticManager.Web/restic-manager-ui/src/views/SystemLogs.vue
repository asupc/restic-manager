<template>
  <div class="logs-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div>
        <h2 class="page-header__title">系统日志</h2>
        <div class="page-header__subtitle">查看应用运行日志：备份任务、调度器、仓库操作与异常记录</div>
      </div>
      <div class="page-header__actions">
        <el-button round icon="el-icon-refresh" size="small" :loading="loading" @click="fetchLogs">刷新</el-button>
      </div>
    </div>

    <!-- 筛选卡片 -->
    <el-card class="filter-card mb-4" shadow="never">
      <div class="filter-row">
        <div class="filter-item filter-item--search">
          <el-input
            v-model="filters.keyword"
            placeholder="按消息、类别或异常内容搜索..."
            prefix-icon="el-icon-search"
            clearable
            size="small"
            @input="handleFilterChange"
          />
        </div>

        <div class="filter-item">
          <el-select
            v-model="filters.level"
            placeholder="全部级别"
            clearable
            size="small"
            @change="handleFilterChange"
            style="width: 160px;"
          >
            <el-option label="全部级别" :value="null"></el-option>
            <el-option label="信息及以上" value="Information"></el-option>
            <el-option label="警告及以上" value="Warning"></el-option>
            <el-option label="错误及以上" value="Error"></el-option>
            <el-option label="仅严重错误" value="Critical"></el-option>
          </el-select>
        </div>

        <div class="filter-item">
          <el-date-picker
            v-model="dateRange"
            type="daterange"
            size="small"
            value-format="yyyy-MM-dd"
            range-separator="至"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            :clearable="true"
            style="width: 260px;"
            @change="handleDateChange"
          />
        </div>

        <div class="filter-item filter-item--reset" v-if="hasActiveFilter">
          <el-button size="small" plain type="info" icon="el-icon-refresh-left" @click="resetFilters">重置筛选</el-button>
        </div>
      </div>
    </el-card>

    <!-- 日志表格 -->
    <el-card shadow="never" class="table-card">
      <div v-if="!loading && logs.length === 0" class="empty">
        <div class="empty__title">{{ hasActiveFilter ? '未找到符合条件的日志' : '暂无系统日志' }}</div>
        <div class="empty__desc">{{ hasActiveFilter ? '可尝试清空筛选条件或调整搜索关键字' : '应用运行产生的日志将展示在此处（按天保留）' }}</div>
        <el-button v-if="hasActiveFilter" size="small" type="primary" plain class="mt-3" @click="resetFilters">清空筛选</el-button>
      </div>

      <div v-else>
        <el-table
          :data="logs"
          v-loading="loading"
          stripe
          style="width: 100%"
          class="custom-table"
        >
          <!-- 时间 -->
          <el-table-column label="时间" width="180" align="center">
            <template #default="scope">
              <span class="text-mono" style="font-size: 13px;">{{ formatTimestamp(scope.row.timestamp) }}</span>
            </template>
          </el-table-column>

          <!-- 级别 -->
          <el-table-column label="级别" width="110" align="center">
            <template #default="scope">
              <span class="rm-badge" :class="`rm-badge--${getLevelTone(scope.row.level)}`">
                <span class="rm-badge__dot"></span>
                {{ getLevelText(scope.row.level) }}
              </span>
            </template>
          </el-table-column>

          <!-- 类别 -->
          <el-table-column label="类别" width="260">
            <template #default="scope">
              <span class="text-mono category-cell" :title="scope.row.category">{{ shortCategory(scope.row.category) }}</span>
            </template>
          </el-table-column>

          <!-- 消息 -->
          <el-table-column label="消息" min-width="300">
            <template #default="scope">
              <span :class="scope.row.exception ? 'text-danger' : ''">{{ scope.row.message }}</span>
            </template>
          </el-table-column>

          <!-- 操作 -->
          <el-table-column label="操作" width="100" align="center" fixed="right">
            <template #default="scope">
              <el-button
                size="mini"
                type="text"
                icon="el-icon-tickets"
                @click="openDetail(scope.row)"
              >
                详情
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
    </el-card>

    <!-- 日志详情弹窗 -->
    <el-dialog
      title="日志详情"
      :visible.sync="detailVisible"
      width="780px"
      append-to-body
      custom-class="log-dialog"
    >
      <div class="log-dialog__header-meta mb-2">
        <span class="text-secondary" style="font-size: 13px;">
          {{ formatTimestamp(currentLog?.timestamp) }} · {{ getLevelText(currentLog?.level) }} · {{ currentLog?.category }}
        </span>
        <el-button size="mini" plain icon="el-icon-document-copy" @click="copyDetail">复制日志</el-button>
      </div>
      <div class="log-terminal">
        <pre class="log-terminal__code"><code>{{ detailText || '（无内容）' }}</code></pre>
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
      detailVisible: false,
      currentLog: null,
      detailText: ''
    };
  },
  computed: {
    hasActiveFilter() {
      return !!(this.filters.keyword || this.filters.level || this.dateRange);
    }
  },
  mounted() {
    this.fetchLogs();
  },
  methods: {
    async fetchLogs() {
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
        this.logs = res.items || [];
        this.total = res.total || 0;
      } catch (e) {
        Message.error('加载日志失败: ' + (e.response?.data?.message || e.message));
      } finally {
        this.loading = false;
      }
    },
    handleFilterChange() {
      if (this.filterTimer) clearTimeout(this.filterTimer);
      this.filterTimer = setTimeout(() => {
        this.page = 1;
        this.fetchLogs();
      }, 300);
    },
    handleDateChange() {
      this.page = 1;
      this.fetchLogs();
    },
    resetFilters() {
      this.filters = { keyword: '', level: null };
      this.dateRange = null;
      this.page = 1;
      this.fetchLogs();
    },
    handlePageChange(newPage) {
      this.page = newPage;
      this.fetchLogs();
    },
    handleSizeChange(newSize) {
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
        case 'Error': return 'danger';
        case 'Warning': return 'warning';
        case 'Information': return 'info';
        default: return 'neutral';
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
    shortCategory(category) {
      if (!category) return '-';
      return category.replace('ResticManager.', '');
    },
    openDetail(row) {
      this.currentLog = row;
      let text = row.message || '';
      if (row.exception) {
        text += '\n\n—— 异常信息 ——\n' + row.exception;
      }
      this.detailText = text;
      this.detailVisible = true;
    },
    async copyDetail() {
      try {
        await navigator.clipboard.writeText(this.detailText);
        Message.success('日志已复制到剪贴板');
      } catch {
        Message.info('请手动选中文本复制');
      }
    }
  }
};
</script>

<style scoped>
.logs-page {
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

.category-cell {
  font-size: 12px;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: block;
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

.rm-badge__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
  display: inline-block;
}
</style>
