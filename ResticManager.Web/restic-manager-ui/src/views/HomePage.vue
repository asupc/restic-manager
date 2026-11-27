<template>
  <div class="home-page">
    <!-- 顶部欢迎栏 -->
    <div class="rm-surface welcome-banner">
      <div class="welcome-banner__left">
        <div class="welcome-banner__title-row">
          <h1 class="welcome-banner__title">您好，{{ userName }}</h1>
          <span class="rm-status-pill" :class="headerHealthPillClass">
            <span class="rm-status-pill__dot"></span>
            {{ headerHealthText }}
          </span>
        </div>
        <div class="welcome-banner__sub">
          {{ greetingTip }} · 今天是 {{ today }}
        </div>
      </div>
      <div class="welcome-banner__actions">
        <el-button type="primary" size="small" round icon="el-icon-plus" @click="$router.push('/tasks?action=new')">
          新建任务
        </el-button>
        <el-button size="small" round icon="el-icon-folder-add" @click="$router.push('/repositories?action=new')">
          新建仓库
        </el-button>
        <el-button size="small" round icon="el-icon-camera" @click="$router.push('/backup-management')">
          快照浏览
        </el-button>
        <el-button size="small" round icon="el-icon-refresh" :loading="refreshing" @click="refreshDashboard">
          刷新数据
        </el-button>
      </div>
    </div>

    <!-- 4 张高质感 KPI 指标卡 -->
    <div class="rm-kpi-grid home-kpi-grid">
      <div
        v-for="card in statCards"
        :key="card.key"
        class="rm-kpi-card home-kpi-card"
        :class="`rm-kpi-card--${card.tone}`"
        @click="card.action && card.action()"
      >
        <div class="home-kpi-card__top">
          <div class="home-kpi-card__icon" :class="`home-kpi-card__icon--${card.tone}`">
            <span v-html="card.icon"></span>
          </div>
          <div class="home-kpi-card__body">
            <div class="rm-kpi-card__label">
              <span>{{ card.label }}</span>
              <i class="el-icon-arrow-right home-kpi-card__arrow"></i>
            </div>
            <div class="home-kpi-card__val-row">
              <span class="rm-kpi-card__value home-kpi-card__num">
                {{ displayValues[card.key] !== undefined ? displayValues[card.key] : card.value }}
              </span>
              <span v-if="card.unit" class="home-kpi-card__unit">{{ card.unit }}</span>
            </div>
          </div>
        </div>
        <div class="rm-kpi-card__meta">
          <i :class="card.trendIcon"></i>
          <span>{{ card.trend }}</span>
        </div>
      </div>
    </div>

    <!-- 均衡 2 列分析区：趋势折线图 + 任务状态环图 -->
    <el-row :gutter="20" class="mb-4">
      <el-col :xs="24" :lg="15">
        <div class="rm-surface rm-surface--padded analytics-card" v-loading="!loaded">
          <div class="card-toolbar">
            <div>
              <div class="card-toolbar__title">备份趋势</div>
              <div class="card-toolbar__sub">最近 {{ trendRange }} 天每日备份执行概况</div>
            </div>
            <el-radio-group v-model="trendRange" size="mini" @change="rebuildTrend">
              <el-radio-button label="7">7 天</el-radio-button>
              <el-radio-button label="14">14 天</el-radio-button>
              <el-radio-button label="30">30 天</el-radio-button>
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
            <div class="trend-summary__item trend-summary__item--right">
              <span class="trend-summary__label">周期成功率</span>
              <span class="trend-summary__value text-success">{{ trendSummary.rate }}%</span>
            </div>
          </div>

          <line-chart
            :data="trendData"
            :labels="trendLabels"
            series-label="成功备份"
            :height="236"
          />
        </div>
      </el-col>

      <el-col :xs="24" :lg="9">
        <div class="rm-surface rm-surface--padded analytics-card" v-loading="!loaded">
          <div class="card-toolbar mb-3">
            <div>
              <div class="card-toolbar__title">任务与执行分布</div>
              <div class="card-toolbar__sub">按任务启用与记录结果汇总</div>
            </div>
          </div>
          <donut-chart :data="statusDistribution" total-label="总计" />
        </div>
      </el-col>
    </el-row>

    <!-- 最近备份记录 + 系统健康与待办告警 -->
    <el-row :gutter="20">
      <el-col :xs="24" :lg="15">
        <div class="rm-surface recent-card">
          <div class="recent-card__header">
            <div>
              <div class="card-toolbar__title">最近备份记录</div>
              <div class="card-toolbar__sub">展示最新 6 条备份任务执行结果</div>
            </div>
            <el-button type="text" size="small" @click="$router.push('/records')">
              查看全部 <i class="el-icon-arrow-right"></i>
            </el-button>
          </div>

          <div v-if="recentTasks.length === 0" class="empty-recent">
            <div class="empty-recent__icon"><i class="el-icon-tickets"></i></div>
            <div class="empty-recent__title">暂无备份记录</div>
            <div class="empty-recent__desc">创建并运行一个备份任务后，最新结果将实时呈现于此</div>
            <el-button type="primary" plain size="small" round class="mt-3" @click="$router.push('/tasks?action=new')">
              创建第一个任务
            </el-button>
          </div>

          <el-table v-else :data="recentTasks" stripe style="width: 100%">
            <el-table-column label="任务与仓库" min-width="200">
              <template #default="scope">
                <div class="task-cell">
                  <div class="task-cell__icon" :class="`task-cell__icon--${getStatusTone(scope.row.status)}`">
                    <i :class="getStatusIcon(scope.row.status)"></i>
                  </div>
                  <div class="task-cell__body">
                    <div class="task-cell__name" :title="scope.row.taskName">{{ scope.row.taskName }}</div>
                    <div class="task-cell__sub">
                      <i class="el-icon-box"></i>
                      {{ scope.row.task?.repositoryName || '未关联仓库' }}
                    </div>
                  </div>
                </div>
              </template>
            </el-table-column>

            <el-table-column label="状态" width="112" align="center">
              <template #default="scope">
                <span class="rm-status-pill" :class="getStatusPillClass(scope.row.status)">
                  <span class="rm-status-pill__dot"></span>
                  {{ getStatusText(scope.row.status) }}
                </span>
              </template>
            </el-table-column>

            <el-table-column label="开始时间" width="164" align="center">
              <template #default="scope">
                <span class="text-mono text-secondary" style="font-size: 12px;">{{ formatDate(scope.row.startTime) }}</span>
              </template>
            </el-table-column>

            <el-table-column label="耗时" width="94" align="right">
              <template #default="scope">
                <span class="text-mono num-val">{{ calculateDuration(scope.row.startTime, scope.row.endTime) }}</span>
              </template>
            </el-table-column>

            <el-table-column label="处理文件" width="96" align="right">
              <template #default="scope">
                <span class="text-mono num-val text-bold">{{ scope.row.filesProcessed || 0 }}</span>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </el-col>

      <el-col :xs="24" :lg="9">
        <!-- 系统健康摘要 -->
        <div class="rm-surface rm-surface--padded mb-4">
          <div class="card-toolbar mb-3">
            <div>
              <div class="card-toolbar__title">系统健康</div>
              <div class="card-toolbar__sub">基于最近 24 小时运行与仓库校验状态</div>
            </div>
            <span class="rm-status-pill" :class="`rm-status-pill--${systemHealth.tone}`">
              <span class="rm-status-pill__dot"></span>
              {{ systemHealth.text }}
            </span>
          </div>
          <ul class="health-list">
            <li v-for="item in healthItems" :key="item.key">
              <div class="health-list__left">
                <span class="health-list__dot" :class="`health-list__dot--${item.tone}`"></span>
                <span>{{ item.label }}</span>
              </div>
              <span class="health-list__value text-mono">{{ item.value }}</span>
            </li>
          </ul>
        </div>

        <!-- 待办与告警卡片 -->
        <div class="rm-surface rm-surface--padded">
          <div class="card-toolbar mb-3">
            <div>
              <div class="card-toolbar__title">待办与告警</div>
              <div class="card-toolbar__sub">自动检测需要立即处理的异常与提醒</div>
            </div>
          </div>
          <ul class="todo-list">
            <li
              v-for="(t, idx) in todos"
              :key="idx"
              :class="['todo-item', `todo-item--${t.tone}`, { 'todo-item--clickable': !!t.action }]"
              @click="t.action && t.action()"
            >
              <span class="todo-item__icon"><i :class="t.icon"></i></span>
              <div class="todo-item__body">
                <div class="todo-item__title">{{ t.title }}</div>
                <div class="todo-item__desc">{{ t.desc }}</div>
              </div>
              <i v-if="t.action" class="el-icon-arrow-right todo-item__arrow"></i>
            </li>
          </ul>
        </div>
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
      refreshing: false,
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
      if (h < 6) return '夜深了，注意休息';
      if (h < 12) return '早上好，新的一天从可靠备份开始';
      if (h < 18) return '下午好，持续守护数据安全';
      return '晚上好，今日的备份计划运行平稳吗';
    },
    headerHealthPillClass() {
      const repoFailed = (this.healthOverview && this.healthOverview.failedRepositories) || 0;
      if (this.systemHealth.tone === 'danger' || repoFailed > 0) return 'rm-status-pill--danger';
      if (this.systemHealth.tone === 'warning') return 'rm-status-pill--warning';
      return 'rm-status-pill--success';
    },
    headerHealthText() {
      const repoFailed = (this.healthOverview && this.healthOverview.failedRepositories) || 0;
      if (this.systemHealth.tone === 'danger' || repoFailed > 0) return '有失败告警';
      if (this.systemHealth.tone === 'warning') return '有关注告警';
      return '全部正常';
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
          trend: '实时执行统计',
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
          trend: `累计处理 ${this.totalFiles} 个文件`,
          trendIcon: 'el-icon-document',
          action: () => this.$router.push('/records')
        },
        {
          key: 'storage',
          tone: 'info',
          label: '已配置仓库',
          value: this.allRepos.length,
          unit: '个',
          icon: ICON_STORAGE,
          trend: `${this.allRepos.filter(r => r.isInitialized).length} 个已初始化`,
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
    this._rafIds = [];
    this.loadUser();
    await this.fetchDashboardData();
  },
  beforeDestroy() {
    this._destroyed = true;
    if (this._rafIds && this._rafIds.length) {
      this._rafIds.forEach(id => cancelAnimationFrame(id));
      this._rafIds = [];
    }
  },
  methods: {
    loadUser() {
      try {
        const raw = localStorage.getItem('user');
        if (raw) {
          const u = JSON.parse(raw);
          this.userName = u.username || u.name || '用户';
        }
      } catch (e) { /* 用户名解析失败，保持默认 */ }
    },
    async refreshDashboard() {
      this.refreshing = true;
      try {
        await this.fetchDashboardData();
      } finally {
        if (!this._destroyed) this.refreshing = false;
      }
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
        if (this._destroyed) return;
        this.allTasks = tasks || [];
        this.allRecords = records || [];
        this.allRepos = repos || [];
        this.allRules = rules || [];
        this.healthOverview = healthOverview;

        this.totalFiles = this.allRecords.reduce((s, r) => s + (r.filesProcessed || 0), 0);

        const taskMap = new Map(this.allTasks.map(t => [t.id, t]));
        const repoMap = new Map(this.allRepos.map(rr => [rr.id, rr]));
        this.recentTasks = [...this.allRecords]
          .sort((a, b) => new Date(b.startTime) - new Date(a.startTime))
          .slice(0, 6)
          .map(r => {
            const taskId = r.backupTaskId || r.taskId;
            const matchedTask = taskMap.get(taskId);
            const matchedRepo = matchedTask ? repoMap.get(matchedTask.repositoryId) : null;
            return {
              ...r,
              taskName: matchedTask ? matchedTask.name : '已删除任务',
              task: {
                ...(matchedTask || {}),
                repositoryName: matchedRepo ? matchedRepo.name : ''
              }
            };
          });

        const enabled = this.allTasks.filter(t => t.isEnabled).length;
        const disabled = this.allTasks.length - enabled;
        const success = this.allRecords.filter(r => r.status === 'Succeeded').length;
        const failed = this.allRecords.filter(r => r.status === 'Failed').length;
        this.statusDistribution = [
          { label: '启用任务', value: enabled },
          { label: '禁用任务', value: disabled },
          { label: '成功记录', value: success },
          { label: '失败记录', value: failed }
        ];

        const last24 = new Date(Date.now() - 24 * 3600 * 1000);
        const recentFailed = this.allRecords.filter(r => r.status === 'Failed' && new Date(r.startTime) >= last24).length;
        if (recentFailed === 0) this.systemHealth = { tone: 'success', text: '正常' };
        else if (recentFailed <= 2) this.systemHealth = { tone: 'warning', text: '需关注' };
        else this.systemHealth = { tone: 'danger', text: '异常' };

        const repoPassed = (healthOverview && healthOverview.passedRepositories) || 0;
        const repoFailed = (healthOverview && healthOverview.failedRepositories) || 0;
        const repoNever = (healthOverview && healthOverview.neverCheckedRepositories) || 0;
        const repoInit = (healthOverview && healthOverview.initializedRepositories) || 0;
        this.healthItems = [
          { key: 'running', label: '运行中任务', value: this.allRecords.filter(r => r.status === 'Running').length, tone: 'warning' },
          { key: 'success24', label: '24h 成功备份', value: this.allRecords.filter(r => r.status === 'Succeeded' && new Date(r.startTime) >= last24).length, tone: 'success' },
          { key: 'failed24', label: '24h 失败告警', value: recentFailed, tone: 'danger' },
          { key: 'rules', label: '已配置规则数', value: this.allRules.length, tone: 'info' },
          { key: 'repoHealth', label: '仓库健康通过', value: `${repoPassed}/${repoInit}`, tone: repoFailed > 0 ? 'danger' : (repoNever > 0 ? 'warning' : 'success') }
        ];

        this.todos = [];
        const neverRun = this.allTasks.filter(t => t.isEnabled && (!t.lastRunAt || t.lastRunStatus === 'NeverRun'));
        if (neverRun.length) {
          this.todos.push({
            tone: 'warning',
            icon: 'el-icon-warning-outline',
            title: `${neverRun.length} 个启用任务尚未执行`,
            desc: '点击查看任务列表并手动触发验证',
            action: () => this.$router.push('/tasks')
          });
        }
        if (recentFailed > 0) {
          this.todos.push({
            tone: 'danger',
            icon: 'el-icon-circle-close',
            title: `最近 24 小时有 ${recentFailed} 次备份失败`,
            desc: '建议立即查看失败日志定位原因',
            action: () => this.$router.push('/records')
          });
        }
        const uninitialized = this.allRepos.filter(r => !r.isInitialized);
        if (uninitialized.length) {
          this.todos.push({
            tone: 'info',
            icon: 'el-icon-folder',
            title: `${uninitialized.length} 个仓库未初始化`,
            desc: '前往仓库页完成初始化后方可写入快照',
            action: () => this.$router.push('/repositories')
          });
        }
        // 仓库健康待办
        if (healthOverview) {
          const failedItems = (healthOverview.attentionItems || []).filter(i => i.status === 'Failed');
          if (failedItems.length) {
            this.todos.push({
              tone: 'danger',
              icon: 'el-icon-warning',
              title: `${failedItems.length} 个仓库健康检查失败`,
              desc: failedItems.slice(0, 2).map(i => `${i.repositoryName}: ${i.lastError || '异常'}`).join('；'),
              action: () => this.$router.push('/repositories')
            });
          }
          if ((healthOverview.neverCheckedRepositories || 0) > 0) {
            this.todos.push({
              tone: 'warning',
              icon: 'el-icon-time',
              title: `${healthOverview.neverCheckedRepositories} 个仓库尚未执行健康检查`,
              desc: '默认每天凌晨 3 点自动检查，也可在仓库页手动触发',
              action: () => this.$router.push('/repositories')
            });
          }
        }
        if (this.todos.length === 0) {
          this.todos.push({
            tone: 'success',
            icon: 'el-icon-success',
            title: '系统状态良好',
            desc: '所有仓库与定时任务均运行平稳，暂无待办事项',
            action: null
          });
        }

        this.rebuildTrend();
        this.loaded = true;
        this.$nextTick(() => {
          if (!this._destroyed) this.animateCounts();
        });
      } catch (err) {
        if (this._destroyed) return;
        this.$message.error('获取仪表盘数据失败: ' + err.message);
        console.error(err);
        this.loaded = true;
      }
    },
    animateCounts() {
      if (this._destroyed) return;
      if (this._rafIds && this._rafIds.length) {
        this._rafIds.forEach(id => cancelAnimationFrame(id));
      }
      this._rafIds = [];
      const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      this.statCards.forEach(card => {
        const target = Number(card.value) || 0;
        if (reduce || target <= 0) {
          this.$set(this.displayValues, card.key, target);
          return;
        }
        const duration = 700;
        const start = performance.now();
        const step = now => {
          if (this._destroyed) return;
          const p = Math.min((now - start) / duration, 1);
          const eased = 1 - Math.pow(1 - p, 3);
          this.$set(this.displayValues, card.key, Math.round(target * eased));
          if (p < 1) {
            const nextId = requestAnimationFrame(step);
            this._rafIds.push(nextId);
          }
        };
        const firstId = requestAnimationFrame(step);
        this._rafIds.push(firstId);
      });
    },
    rebuildTrend() {
      const days = parseInt(this.trendRange, 10) || 14;
      const toDateKey = d => {
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${y}-${m}-${day}`;
      };
      const now = new Date();
      const buckets = [];
      const bucketMap = new Map();
      for (let i = days - 1; i >= 0; i--) {
        const d = new Date(now);
        d.setHours(0, 0, 0, 0);
        d.setDate(d.getDate() - i);
        const key = toDateKey(d);
        const bucket = {
          key,
          label: `${d.getMonth() + 1}/${d.getDate()}`,
          success: 0,
          failed: 0
        };
        buckets.push(bucket);
        bucketMap.set(key, bucket);
      }

      let totalRunning = 0;
      for (let i = 0; i < this.allRecords.length; i++) {
        const r = this.allRecords[i];
        if (r.status === 'Running') totalRunning++;
        if (!r.startTime) continue;
        const t = new Date(r.startTime);
        if (isNaN(t.getTime())) continue;
        const bucket = bucketMap.get(toDateKey(t));
        if (!bucket) continue;
        if (r.status === 'Succeeded') bucket.success++;
        else if (r.status === 'Failed') bucket.failed++;
      }

      this.trendLabels = buckets.map(b => b.label);
      this.trendData = buckets.map(b => b.success);

      const totalSuccess = buckets.reduce((s, b) => s + b.success, 0);
      const totalFailed = buckets.reduce((s, b) => s + b.failed, 0);
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
      const s = Math.max(0, Math.floor(ms / 1000));
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
    getStatusPillClass(status) {
      if (status === 'Running') return 'rm-status-pill--running';
      if (status === 'Succeeded') return 'rm-status-pill--success';
      if (status === 'Failed') return 'rm-status-pill--danger';
      if (status === 'Pending') return 'rm-status-pill--info';
      return 'rm-status-pill--neutral';
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
.home-page {
  padding-bottom: 24px;
}

/* 精致顶部欢迎栏（双主题自适应） */
.welcome-banner {
  position: relative;
  padding: 22px 26px;
  margin-bottom: var(--space-5);
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 18px;
  background: linear-gradient(125deg, var(--brand-50) 0%, var(--color-bg-card) 58%, var(--color-bg-card) 100%);
}

.welcome-banner::before {
  content: "";
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 2px;
  background: linear-gradient(90deg, var(--brand-400), var(--info-500), transparent 85%);
}

.welcome-banner__left {
  min-width: 0;
}

.welcome-banner__title-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.welcome-banner__title {
  margin: 0;
  font-size: var(--font-2xl);
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: -0.02em;
  line-height: 1.25;
}

.welcome-banner__sub {
  margin-top: 6px;
  font-size: var(--font-sm);
  color: var(--color-text-secondary);
}

.welcome-banner__actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

/* 4 张高质感 KPI 指标卡 */
.home-kpi-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.home-kpi-card {
  cursor: pointer;
}

.home-kpi-card__top {
  display: flex;
  align-items: center;
  gap: 14px;
}

.home-kpi-card__icon {
  width: 46px;
  height: 46px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  border: 1px solid var(--color-border-soft);
}

.home-kpi-card__icon svg {
  width: 22px;
  height: 22px;
}

.home-kpi-card__icon--brand {
  background: linear-gradient(135deg, var(--brand-100), var(--brand-50));
  color: var(--brand-400);
}

.home-kpi-card__icon--warning {
  background: linear-gradient(135deg, var(--warning-50), var(--color-bg-soft));
  color: var(--warning-500);
}

.home-kpi-card__icon--success {
  background: linear-gradient(135deg, var(--success-50), var(--color-bg-soft));
  color: var(--success-500);
}

.home-kpi-card__icon--info {
  background: linear-gradient(135deg, var(--info-50), var(--color-bg-soft));
  color: var(--info-500);
}

.home-kpi-card__body {
  flex: 1;
  min-width: 0;
}

.home-kpi-card__arrow {
  color: var(--color-text-placeholder);
  transition: transform var(--transition-fast), color var(--transition-fast);
}

.home-kpi-card:hover .home-kpi-card__arrow {
  transform: translateX(3px);
  color: var(--brand-400);
}

.home-kpi-card__val-row {
  display: flex;
  align-items: baseline;
  gap: 5px;
  margin-top: 4px;
}

.home-kpi-card__num {
  margin-top: 0;
  font-size: 28px;
  font-variant-numeric: tabular-nums;
}

.home-kpi-card__unit {
  font-size: var(--font-xs);
  color: var(--color-text-secondary);
  font-weight: 500;
}

/* 分析卡片 */
.analytics-card {
  height: 100%;
  margin-bottom: 16px;
}

.card-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.card-toolbar__title {
  font-size: var(--font-md);
  font-weight: 600;
  color: var(--color-text-primary);
}

.card-toolbar__sub {
  font-size: var(--font-xs);
  color: var(--color-text-secondary);
  margin-top: 2px;
}

/* 趋势摘要 */
.trend-summary {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 18px;
  padding: 12px 0 14px;
  border-bottom: 1px dashed var(--color-border);
  margin-bottom: 10px;
}

.trend-summary__item {
  display: flex;
  align-items: center;
  gap: 7px;
  font-size: 13px;
  color: var(--color-text-secondary);
}

.trend-summary__item--right {
  margin-left: auto;
}

.trend-summary__label {
  color: var(--color-text-secondary);
}

.trend-summary__value {
  font-weight: 700;
  color: var(--color-text-primary);
  font-size: 15px;
  font-variant-numeric: tabular-nums;
}

.trend-dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  display: inline-block;
}

.trend-dot--success { background: var(--success-500); }
.trend-dot--danger { background: var(--danger-500); }
.trend-dot--warning { background: var(--warning-500); }

/* 最近记录卡片 */
.recent-card {
  margin-bottom: 16px;
}

.recent-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-border-soft);
}

.task-cell {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
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
.task-cell__icon--neutral { background: var(--gray-100); color: var(--color-text-secondary); }

.task-cell__body {
  min-width: 0;
  flex: 1;
}

.task-cell__name {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 13.5px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-cell__sub {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 2px;
  display: flex;
  align-items: center;
  gap: 4px;
}

.num-val {
  font-variant-numeric: tabular-nums;
  font-size: 13px;
  color: var(--color-text-primary);
}

/* 空状态 */
.empty-recent {
  text-align: center;
  padding: 48px 20px;
  color: var(--color-text-secondary);
}

.empty-recent__icon {
  width: 48px;
  height: 48px;
  border-radius: var(--radius-lg);
  background: var(--brand-50);
  color: var(--brand-400);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  margin-bottom: 10px;
}

.empty-recent__title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.empty-recent__desc {
  font-size: 12px;
  margin-top: 4px;
  color: var(--color-text-secondary);
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
  padding: 9px 0;
  border-bottom: 1px dashed var(--color-border-soft);
  font-size: 13.5px;
}

.health-list li:last-child {
  border-bottom: 0;
}

.health-list__left {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-regular);
}

.health-list__dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.health-list__dot--success { background: var(--success-500); }
.health-list__dot--warning { background: var(--warning-500); }
.health-list__dot--danger { background: var(--danger-500); }
.health-list__dot--info { background: var(--info-500); }

.health-list__value {
  font-weight: 700;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

/* 待办列表 */
.todo-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.todo-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 11px 12px;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  margin-bottom: 8px;
  transition: background-color var(--transition-fast), transform var(--transition-fast), border-color var(--transition-fast);
  border: 1px solid var(--color-border-soft);
  border-left: 3px solid transparent;
}

.todo-item:last-child {
  margin-bottom: 0;
}

.todo-item--clickable {
  cursor: pointer;
}

.todo-item--clickable:hover {
  background: var(--color-bg-hover);
  transform: translateX(2px);
}

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
  font-size: 15px;
}

.todo-item--warning .todo-item__icon { color: var(--warning-600); }
.todo-item--danger .todo-item__icon { color: var(--danger-600); }
.todo-item--info .todo-item__icon { color: var(--info-600); }
.todo-item--success .todo-item__icon { color: var(--success-600); }

.todo-item__body {
  flex: 1;
  min-width: 0;
}

.todo-item__title {
  font-size: 13.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.todo-item__desc {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 2px;
  line-height: 1.45;
}

.todo-item__arrow {
  color: var(--color-text-placeholder);
  font-size: 14px;
}

@media (max-width: 1100px) {
  .home-kpi-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .home-kpi-grid {
    grid-template-columns: 1fr;
  }
  .welcome-banner {
    padding: 18px;
  }
  .trend-summary__item--right {
    margin-left: 0;
  }
}
</style>