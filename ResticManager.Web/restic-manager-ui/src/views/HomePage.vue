<template>
  <div class="home-page">
    <!-- 顶部欢迎横幅 -->
    <div class="welcome-banner">
      <div class="welcome-banner__content">
        <div class="welcome-banner__title">
          您好，{{ userName }}
        </div>
        <div class="welcome-banner__sub">
          {{ greetingTip }} · 今天是 {{ today }}
        </div>
      </div>
      <div class="welcome-banner__actions">
        <el-button type="primary" icon="el-icon-plus" round @click="$router.push('/tasks?action=new')">
          新建备份任务
        </el-button>
        <el-button round icon="el-icon-folder-add" @click="$router.push('/repositories?action=new')">
          添加仓库
        </el-button>
      </div>
    </div>

    <!-- 统计卡片 -->
    <el-row :gutter="20" class="stat-row">
      <el-col :xs="12" :sm="12" :md="6" v-for="card in statCards" :key="card.key">
        <div class="stat-card" :class="`stat-card--${card.tone}`" @click="card.action && card.action()">
          <div class="stat-card__icon">
            <span v-html="card.icon"></span>
          </div>
          <div class="stat-card__body">
            <div class="stat-card__label">{{ card.label }}</div>
            <div class="stat-card__value">
              <span class="stat-card__num">{{ displayValues[card.key] !== undefined ? displayValues[card.key] : card.value }}</span>
              <span v-if="card.unit" class="stat-card__unit">{{ card.unit }}</span>
            </div>
            <div class="stat-card__trend">
              <i :class="card.trendIcon"></i>
              <span>{{ card.trend }}</span>
            </div>
          </div>
          <div class="stat-card__bg"></div>
        </div>
      </el-col>
    </el-row>

    <!-- 主图表 + 状态 -->
    <el-row :gutter="20" class="mt-4">
      <el-col :xs="24" :lg="16">
        <el-card class="is-hover-shadow" v-loading="!loaded">
          <div slot="header" class="card-toolbar">
            <div>
              <div class="card-toolbar__title">备份趋势</div>
              <div class="card-toolbar__sub">最近 14 天每日备份任务结果</div>
            </div>
            <el-radio-group v-model="trendRange" size="mini" @change="rebuildTrend">
              <el-radio-button label="7">7天</el-radio-button>
              <el-radio-button label="14">14天</el-radio-button>
              <el-radio-button label="30">30天</el-radio-button>
            </el-radio-group>
          </div>
          <div class="trend-summary">
            <div class="trend-summary__item">
              <span class="trend-dot trend-dot--success"></span>
              <span class="trend-summary__label">成功</span>
              <span class="trend-summary__value">{{ trendSummary.success }}</span>
            </div>
            <div class="trend-summary__item">
              <span class="trend-dot trend-dot--danger"></span>
              <span class="trend-summary__label">失败</span>
              <span class="trend-summary__value">{{ trendSummary.failed }}</span>
            </div>
            <div class="trend-summary__item">
              <span class="trend-dot trend-dot--warning"></span>
              <span class="trend-summary__label">运行中</span>
              <span class="trend-summary__value">{{ trendSummary.running }}</span>
            </div>
            <div class="trend-summary__item">
              <span class="trend-summary__label">成功率</span>
              <span class="trend-summary__value text-success text-bold">{{ trendSummary.rate }}%</span>
            </div>
          </div>
          <line-chart
            :data="trendData"
            :labels="trendLabels"
            color="#06b6d4"
            :height="240"
          />
        </el-card>
      </el-col>

      <el-col :xs="24" :lg="8">
        <el-card class="is-hover-shadow donut-card" v-loading="!loaded">
          <div slot="header" class="card-toolbar">
            <div>
              <div class="card-toolbar__title">任务分布</div>
              <div class="card-toolbar__sub">按状态统计</div>
            </div>
          </div>
          <donut-chart :data="statusDistribution" total-label="任务" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 最近任务 + 系统状态 -->
    <el-row :gutter="20" class="mt-4">
      <el-col :xs="24" :lg="16">
        <el-card class="is-hover-shadow">
          <div slot="header" class="card-toolbar">
            <div>
              <div class="card-toolbar__title">最近备份记录</div>
              <div class="card-toolbar__sub">显示最新 6 条执行结果</div>
            </div>
            <el-button type="text" icon="el-icon-arrow-right" @click="$router.push('/records')">
              查看全部
            </el-button>
          </div>
          <div v-if="recentTasks.length === 0" class="empty-recent">
            <div>暂无备份记录</div>
            <el-button type="primary" plain size="small" class="mt-3" @click="$router.push('/tasks?action=new')">
              创建第一个任务
            </el-button>
          </div>
          <el-table v-else :data="recentTasks" stripe style="width:100%" :show-header="true" size="default">
            <el-table-column label="任务" min-width="180">
              <template #default="scope">
                <div class="task-cell">
                  <div class="task-cell__icon" :class="`task-cell__icon--${getStatusTone(scope.row.status)}`">
                    <i :class="getStatusIcon(scope.row.status)"></i>
                  </div>
                  <div>
                    <div class="task-cell__name">{{ scope.row.taskName }}</div>
                    <div class="task-cell__sub">{{ scope.row.task?.repositoryName || '仓库' }}</div>
                  </div>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100" align="center">
              <template #default="scope">
                <span class="rm-badge" :class="`rm-badge--${getStatusTone(scope.row.status)}`">
                  {{ getStatusText(scope.row.status) }}
                </span>
              </template>
            </el-table-column>
            <el-table-column label="开始时间" width="160" align="center">
              <template #default="scope">{{ formatDate(scope.row.startTime) }}</template>
            </el-table-column>
            <el-table-column label="耗时" width="100" align="center">
              <template #default="scope">
                <span class="text-mono">{{ calculateDuration(scope.row.startTime, scope.row.endTime) }}</span>
              </template>
            </el-table-column>
            <el-table-column label="处理文件" width="100" align="center">
              <template #default="scope">
                <span class="text-bold">{{ scope.row.filesProcessed || 0 }}</span>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>

      <el-col :xs="24" :lg="8">
        <!-- 系统健康 + 待办 -->
        <el-card class="is-hover-shadow health-card mb-4">
          <div slot="header" class="card-toolbar">
            <div>
              <div class="card-toolbar__title">系统健康</div>
              <div class="card-toolbar__sub">基于最近 24 小时状态</div>
            </div>
            <span class="rm-badge" :class="`rm-badge--${systemHealth.tone}`">
              {{ systemHealth.text }}
            </span>
          </div>
          <ul class="health-list">
            <li v-for="item in healthItems" :key="item.key">
              <div class="health-list__left">
                <span class="health-list__dot" :class="`health-list__dot--${item.tone}`"></span>
                <span>{{ item.label }}</span>
              </div>
              <span class="health-list__value text-bold">{{ item.value }}</span>
            </li>
          </ul>
        </el-card>

        <el-card class="is-hover-shadow">
          <div slot="header" class="card-toolbar">
            <div>
              <div class="card-toolbar__title">待办提醒</div>
              <div class="card-toolbar__sub">系统检测到需要关注的事项</div>
            </div>
          </div>
          <ul class="todo-list">
            <li v-if="todos.length === 0" class="todo-empty">
              一切正常,无需处理
            </li>
            <li v-for="(t, idx) in todos" :key="idx" :class="['todo-item', `todo-item--${t.tone}`]" @click="t.action && t.action()">
              <span class="todo-item__icon"><i :class="t.icon"></i></span>
              <div class="todo-item__body">
                <div class="todo-item__title">{{ t.title }}</div>
                <div class="todo-item__desc">{{ t.desc }}</div>
              </div>
              <i class="el-icon-arrow-right todo-item__arrow"></i>
            </li>
          </ul>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script>
import { backupService } from '@/services/backup.service';
import { backupRecordService } from '@/services/backup-record.service';
import { repositoryService } from '@/services/repository.service';
import { backupRuleService } from '@/services/backup-rule.service';
import LineChart from '@/components/charts/LineChart.vue';
import DonutChart from '@/components/charts/DonutChart.vue';

const ICON_TASKS = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>';
const ICON_RUNNING = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>';
const ICON_SUCCESS = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>';
const ICON_STORAGE = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/></svg>';

export default {
  components: { LineChart, DonutChart },
  data() {
    return {
      loaded: false,
      userName: '用户',
      trendRange: '14',
      displayValues: {},
      allTasks: [],
      allRecords: [],
      allRepos: [],
      allRules: [],
      healthOverview: null,
      recentTasks: [],
      statusDistribution: [],
      trendData: [],
      trendLabels: [],
      totalFiles: 0,
      totalStorageBytes: 0,
      trendSummary: { success: 0, failed: 0, running: 0, rate: 0 },
      systemHealth: { tone: 'success', text: '正常' },
      healthItems: [],
      todos: []
    };
  },
  computed: {
    today() {
      const d = new Date();
      const w = ['日', '一', '二', '三', '四', '五', '六'][d.getDay()];
      return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日 星期${w}`;
    },
    greetingTip() {
      const h = new Date().getHours();
      if (h < 6) return '夜深了,注意休息';
      if (h < 12) return '早上好,新的一天从备份开始';
      if (h < 18) return '下午好,持续守护数据安全';
      return '晚上好,今日的备份已完成了吗';
    },
    statCards() {
      return [
        {
          key: 'tasks',
          tone: 'brand',
          label: '备份任务总数',
          value: this.allTasks.length,
          unit: '个',
          icon: ICON_TASKS,
          trend: `${this.allTasks.filter(t => t.isEnabled).length} 个已启用`,
          trendIcon: 'el-icon-success',
          action: () => this.$router.push('/tasks')
        },
        {
          key: 'running',
          tone: 'warning',
          label: '运行中任务',
          value: this.allRecords.filter(r => r.status === 'Running').length,
          unit: '个',
          icon: ICON_RUNNING,
          trend: '实时统计',
          trendIcon: 'el-icon-loading',
          action: () => this.$router.push('/tasks')
        },
        {
          key: 'success',
          tone: 'success',
          label: '今日成功备份',
          value: this.todaySuccessCount,
          unit: '次',
          icon: ICON_SUCCESS,
          trend: `共处理 ${this.totalFiles} 个文件`,
          trendIcon: 'el-icon-document',
          action: () => this.$router.push('/backup-management')
        },
        {
          key: 'storage',
          tone: 'info',
          label: '已配置仓库',
          value: this.allRepos.length,
          unit: '个',
          icon: ICON_STORAGE,
          trend: `共 ${this.allRepos.filter(r => r.isInitialized).length} 个已初始化`,
          trendIcon: 'el-icon-box',
          action: () => this.$router.push('/repositories')
        }
      ];
    },
    todaySuccessCount() {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      return this.allRecords.filter(r => {
        const d = new Date(r.startTime);
        return r.status === 'Succeeded' && d >= today;
      }).length;
    }
  },
  async mounted() {
    this.loadUser();
    await this.fetchDashboardData();
  },
  methods: {
    loadUser() {
      try {
        const raw = localStorage.getItem('user');
        if (raw) {
          const u = JSON.parse(raw);
          this.userName = u.username || u.name || '用户';
        }
      } catch (e) { /* 用户名解析失败,保持默认 */ }
    },
    async fetchDashboardData() {
      try {
        const [tasks, records, repos, rules, healthOverview] = await Promise.all([
          backupService.getTasks(),
          backupRecordService.getRecords(),
          repositoryService.getRepositories(),
          backupRuleService.getAllRules(),
          repositoryService.getHealthOverview()
        ]);
        this.allTasks = tasks;
        this.allRecords = records;
        this.allRepos = repos;
        this.allRules = rules;
        this.healthOverview = healthOverview;

        this.totalFiles = records.reduce((s, r) => s + (r.filesProcessed || 0), 0);

        this.recentTasks = [...records]
          .sort((a, b) => new Date(b.startTime) - new Date(a.startTime))
          .slice(0, 6)
          .map(r => ({
            ...r,
            taskName: tasks.find(t => t.id === r.taskId)?.name || '已删除任务',
            task: {
              ...tasks.find(t => t.id === r.taskId),
              repositoryName: repos.find(rr => rr.id === tasks.find(t => t.id === r.taskId)?.repositoryId)?.name
            }
          }));

        const total = tasks.length || 1;
        const enabled = tasks.filter(t => t.isEnabled).length;
        const disabled = total - enabled;
        const success = records.filter(r => r.status === 'Succeeded').length;
        const failed = records.filter(r => r.status === 'Failed').length;
        this.statusDistribution = [
          { label: '启用任务', value: enabled },
          { label: '禁用任务', value: disabled },
          { label: '成功记录', value: success },
          { label: '失败记录', value: failed }
        ];

        const last24 = new Date(Date.now() - 24 * 3600 * 1000);
        const recentFailed = records.filter(r => r.status === 'Failed' && new Date(r.startTime) >= last24).length;
        if (recentFailed === 0) this.systemHealth = { tone: 'success', text: '正常' };
        else if (recentFailed <= 2) this.systemHealth = { tone: 'warning', text: '需关注' };
        else this.systemHealth = { tone: 'danger', text: '异常' };

        const repoPassed = (healthOverview && healthOverview.passedRepositories) || 0;
        const repoFailed = (healthOverview && healthOverview.failedRepositories) || 0;
        const repoNever = (healthOverview && healthOverview.neverCheckedRepositories) || 0;
        const repoInit = (healthOverview && healthOverview.initializedRepositories) || 0;
        this.healthItems = [
          { key: 'running', label: '运行中', value: records.filter(r => r.status === 'Running').length, tone: 'warning' },
          { key: 'success24', label: '24h 成功', value: records.filter(r => r.status === 'Succeeded' && new Date(r.startTime) >= last24).length, tone: 'success' },
          { key: 'failed24', label: '24h 失败', value: recentFailed, tone: 'danger' },
          { key: 'rules', label: '规则数', value: rules.length, tone: 'info' },
          { key: 'repoHealth', label: '仓库健康', value: `${repoPassed}/${repoInit}`, tone: repoFailed > 0 ? 'danger' : (repoNever > 0 ? 'warning' : 'success') }
        ];

        this.todos = [];
        const neverRun = tasks.filter(t => t.isEnabled && (!t.lastRunAt || t.lastRunStatus === 'NeverRun'));
        if (neverRun.length) {
          this.todos.push({
            tone: 'warning', icon: 'el-icon-warning-outline',
            title: `${neverRun.length} 个启用任务尚未执行`,
            desc: '点击查看任务列表',
            action: () => this.$router.push('/tasks')
          });
        }
        if (recentFailed > 0) {
          this.todos.push({
            tone: 'danger', icon: 'el-icon-circle-close',
            title: `最近 24 小时有 ${recentFailed} 次备份失败`,
            desc: '建议查看失败原因',
            action: () => this.$router.push('/backup-management')
          });
        }
        const uninitialized = repos.filter(r => !r.isInitialized);
        if (uninitialized.length) {
          this.todos.push({
            tone: 'info', icon: 'el-icon-folder',
            title: `${uninitialized.length} 个仓库未初始化`,
            desc: '前往仓库页完成初始化',
            action: () => this.$router.push('/repositories')
          });
        }
        // 仓库健康待办
        if (healthOverview) {
          const failedItems = (healthOverview.attentionItems || []).filter(i => i.status === 'Failed');
          if (failedItems.length) {
            this.todos.push({
              tone: 'danger', icon: 'el-icon-warning',
              title: `${failedItems.length} 个仓库健康检查失败`,
              desc: failedItems.slice(0, 2).map(i => `${i.repositoryName}: ${i.lastError || '异常'}`).join('；'),
              action: () => this.$router.push('/repositories')
            });
          }
          if ((healthOverview.neverCheckedRepositories || 0) > 0) {
            this.todos.push({
              tone: 'warning', icon: 'el-icon-time',
              title: `${healthOverview.neverCheckedRepositories} 个仓库尚未执行健康检查`,
              desc: '默认每天凌晨 3 点自动检查，可在仓库页手动触发',
              action: () => this.$router.push('/repositories')
            });
          }
        }
        if (this.todos.length === 0) {
          this.todos.push({
            tone: 'success', icon: 'el-icon-success',
            title: '系统状态良好',
            desc: '没有需要处理的事项',
            action: null
          });
        }

        this.rebuildTrend();
        this.loaded = true;
        this.$nextTick(() => this.animateCounts());
      } catch (err) {
        this.$message.error('获取仪表盘数据失败: ' + err.message);
        console.error(err);
        this.loaded = true;
      }
    },
    animateCounts() {
      const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      this.statCards.forEach(card => {
        const target = Number(card.value) || 0;
        if (reduce || target <= 0) {
          this.$set(this.displayValues, card.key, target);
          return;
        }
        const duration = 800;
        const start = performance.now();
        const step = now => {
          const p = Math.min((now - start) / duration, 1);
          // easeOutCubic
          const eased = 1 - Math.pow(1 - p, 3);
          this.$set(this.displayValues, card.key, Math.round(target * eased));
          if (p < 1) requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
      });
    },
    rebuildTrend() {
      const days = parseInt(this.trendRange, 10) || 14;
      const now = new Date();
      const buckets = [];
      for (let i = days - 1; i >= 0; i--) {
        const d = new Date(now);
        d.setHours(0, 0, 0, 0);
        d.setDate(d.getDate() - i);
        const next = new Date(d);
        next.setDate(d.getDate() + 1);
        const dayRecords = this.allRecords.filter(r => {
          const t = new Date(r.startTime);
          return t >= d && t < next;
        });
        const success = dayRecords.filter(r => r.status === 'Succeeded').length;
        const failed = dayRecords.filter(r => r.status === 'Failed').length;
        buckets.push({
          label: `${d.getMonth() + 1}/${d.getDate()}`,
          success,
          failed
        });
      }
      this.trendLabels = buckets.map(b => b.label);
      this.trendData = buckets.map(b => b.success);

      const totalSuccess = buckets.reduce((s, b) => s + b.success, 0);
      const totalFailed = buckets.reduce((s, b) => s + b.failed, 0);
      const totalRunning = this.allRecords.filter(r => r.status === 'Running').length;
      const total = totalSuccess + totalFailed;
      this.trendSummary = {
        success: totalSuccess,
        failed: totalFailed,
        running: totalRunning,
        rate: total ? Math.round((totalSuccess / total) * 100) : 100
      };
    },
    formatDate(d) {
      if (!d) return '-';
      return new Date(d).toLocaleString('zh-CN', { hour12: false });
    },
    calculateDuration(start, end) {
      if (!start) return '-';
      if (!end) return '运行中';
      const ms = new Date(end) - new Date(start);
      const s = Math.floor(ms / 1000);
      const m = Math.floor(s / 60);
      const h = Math.floor(m / 60);
      if (h > 0) return `${h}h${m % 60}m`;
      if (m > 0) return `${m}m${s % 60}s`;
      return `${s}s`;
    },
    getStatusTone(status) {
      const map = { Succeeded: 'success', Failed: 'danger', Running: 'warning' };
      return map[status] || 'neutral';
    },
    getStatusIcon(status) {
      const map = { Succeeded: 'el-icon-success', Failed: 'el-icon-error', Running: 'el-icon-loading' };
      return map[status] || 'el-icon-time';
    },
    getStatusText(status) {
      const map = { Succeeded: '成功', Failed: '失败', Running: '运行中', Pending: '排队中' };
      return map[status] || status || '未运行';
    }
  }
};
</script>

<style scoped>
.home-page { padding-bottom: 24px; }

/* 欢迎横幅 */
.welcome-banner {
  position: relative;
  border-radius: var(--radius-xl);
  padding: 28px 32px;
  background: var(--brand-600);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  margin-bottom: 24px;
  overflow: hidden;
}
.welcome-banner__content { z-index: 2; }
.welcome-banner__title {
  font-size: 26px;
  font-weight: 700;
  letter-spacing: -0.01em;
}
.welcome-banner__wave { margin-left: 6px; }
.welcome-banner__sub {
  margin-top: 6px;
  font-size: 14px;
  opacity: 0.85;
}
.welcome-banner__actions { display: flex; gap: 10px; z-index: 2; flex-wrap: wrap; }
.welcome-banner__actions .el-button {
  background: rgba(255, 255, 255, 0.16) !important;
  border: 1px solid rgba(255, 255, 255, 0.3) !important;
  color: #fff !important;
}
.welcome-banner__actions .el-button:hover {
  background: rgba(255, 255, 255, 0.28) !important;
}

/* 统计卡片 */
.stat-row { margin-bottom: 4px; }
.stat-card {
  position: relative;
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 18px 20px;
  background: var(--color-bg-card);
  border-radius: var(--radius-lg);
  box-shadow: none;
  border: 1px solid var(--color-border);
  cursor: pointer;
  transition: border-color var(--transition-base), transform var(--transition-base);
  overflow: hidden;
  margin-bottom: 20px;
}
.stat-card:hover {
  border-color: var(--brand-300);
  transform: translateY(-2px);
}
.stat-card__icon {
  width: 48px;
  height: 48px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.stat-card__icon svg { width: 24px; height: 24px; }
.stat-card__body { flex: 1; min-width: 0; }
.stat-card__label {
  font-size: 13px;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.stat-card__value {
  display: flex;
  align-items: baseline;
  gap: 4px;
  margin-top: 2px;
}
.stat-card__num {
  font-size: 28px;
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: -0.02em;
}
.stat-card__unit {
  font-size: 13px;
  color: var(--color-text-secondary);
  font-weight: 500;
}
.stat-card__trend {
  font-size: 12px;
  color: var(--color-text-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 2px;
}
.stat-card__trend i { font-size: 12px; }

.stat-card__bg {
  position: absolute;
  right: -20px;
  bottom: -30px;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  opacity: 0.08;
  pointer-events: none;
}

.stat-card--brand .stat-card__icon { background: var(--brand-50); color: var(--brand-600); }
.stat-card--brand .stat-card__bg { background: var(--brand-500); }

.stat-card--warning .stat-card__icon { background: var(--warning-50); color: var(--warning-600); }
.stat-card--warning .stat-card__bg { background: var(--warning-500); }

.stat-card--success .stat-card__icon { background: var(--success-50); color: var(--success-600); }
.stat-card--success .stat-card__bg { background: var(--success-500); }

.stat-card--info .stat-card__icon { background: var(--info-50); color: var(--info-600); }
.stat-card--info .stat-card__bg { background: var(--info-500); }

/* 卡片工具栏 */
.card-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.card-toolbar__title {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
}
.card-toolbar__sub {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 2px;
}

/* 趋势图摘要 */
.trend-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 18px;
  padding: 8px 0 16px;
  border-bottom: 1px dashed var(--color-border);
  margin-bottom: 8px;
}
.trend-summary__item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
}
.trend-summary__label { color: var(--color-text-secondary); }
.trend-summary__value {
  font-weight: 700;
  color: var(--color-text-primary);
  font-size: 15px;
}
.trend-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  display: inline-block;
}
.trend-dot--success { background: var(--success-500); }
.trend-dot--danger { background: var(--danger-500); }
.trend-dot--warning { background: var(--warning-500); }

/* 任务列表 cell */
.task-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}
.task-cell__icon {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.task-cell__icon--success { background: var(--success-50); color: var(--success-600); }
.task-cell__icon--danger { background: var(--danger-50); color: var(--danger-600); }
.task-cell__icon--warning { background: var(--warning-50); color: var(--warning-600); }
.task-cell__icon--neutral { background: var(--gray-100); color: var(--gray-500); }
.task-cell__name {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 14px;
}
.task-cell__sub {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 2px;
}

/* 空状态 */
.empty-recent {
  text-align: center;
  padding: 40px 20px;
  color: var(--color-text-secondary);
}
.empty-recent__icon {
  font-size: 56px;
  margin-bottom: 8px;
}

/* 系统健康 */
.health-list {
  list-style: none;
  padding: 0;
  margin: 0;
}
.health-list li {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 0;
  border-bottom: 1px dashed var(--color-border-soft);
  font-size: 14px;
}
.health-list li:last-child { border-bottom: 0; }
.health-list__left { display: flex; align-items: center; gap: 8px; color: var(--color-text-regular); }
.health-list__dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.health-list__dot--success { background: var(--success-500); }
.health-list__dot--warning { background: var(--warning-500); }
.health-list__dot--danger { background: var(--danger-500); }
.health-list__dot--info { background: var(--info-500); }
.health-list__value { color: var(--color-text-primary); }

/* 待办 */
.todo-list { list-style: none; padding: 0; margin: 0; }
.todo-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  margin-bottom: 8px;
  cursor: pointer;
  transition: all var(--transition-fast);
  border-left: 3px solid transparent;
}
.todo-item:hover { background: var(--brand-50); transform: translateX(2px); }
.todo-item--warning { border-left-color: var(--warning-500); }
.todo-item--danger { border-left-color: var(--danger-500); }
.todo-item--info { border-left-color: var(--info-500); }
.todo-item--success { border-left-color: var(--success-500); }
.todo-item__icon {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  background: var(--color-bg-card);
  font-size: 16px;
}
.todo-item--warning .todo-item__icon { color: var(--warning-600); }
.todo-item--danger .todo-item__icon { color: var(--danger-600); }
.todo-item--info .todo-item__icon { color: var(--info-600); }
.todo-item--success .todo-item__icon { color: var(--success-600); }
.todo-item__body { flex: 1; min-width: 0; }
.todo-item__title { font-size: 14px; font-weight: 600; color: var(--color-text-primary); }
.todo-item__desc { font-size: 12px; color: var(--color-text-secondary); margin-top: 2px; }
.todo-item__arrow { color: var(--color-text-placeholder); font-size: 14px; }
.todo-empty {
  text-align: center;
  padding: 24px 0;
  color: var(--color-text-secondary);
  background: var(--color-bg-soft);
  border-radius: var(--radius-md);
  font-size: 14px;
}

@media (max-width: 768px) {
  .welcome-banner { flex-direction: column; align-items: flex-start; padding: 20px; }
  .welcome-banner__title { font-size: 22px; }
  .welcome-banner__deco { display: none; }
  .stat-card { padding: 14px; }
  .stat-card__num { font-size: 22px; }
}
</style>