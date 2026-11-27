<template>
  <div class="tasks-page">
    <!-- 页面头 -->
    <div class="page-header">
      <div>
        <h2 class="page-header__title">备份任务</h2>
        <div class="page-header__subtitle">管理所有自动与手动备份任务、调度计划及历史执行记录</div>
      </div>
      <div class="page-header__actions">
        <el-button round :loading="loading" icon="el-icon-refresh" size="small" @click="refresh">刷新</el-button>
        <el-button type="primary" round icon="el-icon-plus" size="small" @click="openCreateDialog">新建任务</el-button>
      </div>
    </div>

    <!-- 顶部紧凑统计条 -->
    <div class="rm-kpi-grid tasks-kpi-grid">
      <div class="rm-kpi-card rm-kpi-card--brand">
        <div class="rm-kpi-card__label">
          <span>总任务数</span>
          <i class="el-icon-tickets kpi-icon kpi-icon--brand"></i>
        </div>
        <div class="rm-kpi-card__value">{{ tasks.length }}</div>
        <div class="rm-kpi-card__meta">其中 {{ scheduledCount }} 个配置了定时计划</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--success">
        <div class="rm-kpi-card__label">
          <span>已启用</span>
          <i class="el-icon-circle-check kpi-icon kpi-icon--success"></i>
        </div>
        <div class="rm-kpi-card__value">{{ enabledCount }}</div>
        <div class="rm-kpi-card__meta">{{ tasks.length - enabledCount }} 个已暂停</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--info">
        <div class="rm-kpi-card__label">
          <span>运行中</span>
          <i class="el-icon-loading kpi-icon kpi-icon--info"></i>
        </div>
        <div class="rm-kpi-card__value">{{ runningCount }}</div>
        <div class="rm-kpi-card__meta">{{ runningCount > 0 ? '后台正在执行备份' : '当前无执行中任务' }}</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--danger">
        <div class="rm-kpi-card__label">
          <span>最近失败</span>
          <i class="el-icon-warning-outline kpi-icon kpi-icon--danger"></i>
        </div>
        <div class="rm-kpi-card__value">{{ lastFailedCount }}</div>
        <div class="rm-kpi-card__meta">{{ lastFailedCount > 0 ? '建议检查失败日志' : '无最近失败记录' }}</div>
      </div>
    </div>

    <!-- 统一筛选工具条 -->
    <div class="rm-toolbar">
      <div class="rm-toolbar__group">
        <el-input
          v-model="filters.name"
          placeholder="搜索任务名称或源路径..."
          prefix-icon="el-icon-search"
          clearable
          size="small"
          class="toolbar-search"
          @input="handleFilterChange"
        />
        <el-select
          v-model="filters.state"
          placeholder="任务状态"
          clearable
          size="small"
          class="toolbar-select"
          @change="handleFilterChange"
        >
          <el-option label="全部状态" value=""></el-option>
          <el-option label="已启用" value="enabled"></el-option>
          <el-option label="已暂停" value="disabled"></el-option>
          <el-option label="运行中" value="running"></el-option>
          <el-option label="上次失败" value="failed"></el-option>
        </el-select>
        <el-select
          v-model="filters.repositoryId"
          placeholder="目标仓库"
          clearable
          size="small"
          class="toolbar-select"
          @change="handleFilterChange"
        >
          <el-option label="全部仓库" :value="0"></el-option>
          <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id"></el-option>
        </el-select>
        <el-select
          v-model="filters.ruleId"
          placeholder="备份规则"
          clearable
          size="small"
          class="toolbar-select"
          @change="handleFilterChange"
        >
          <el-option label="全部规则" :value="0"></el-option>
          <el-option v-for="r in backupRules" :key="r.id" :label="r.name" :value="r.id"></el-option>
        </el-select>
        <el-button
          v-if="filterActiveCount > 0"
          size="small"
          plain
          icon="el-icon-refresh-left"
          @click="resetFilters"
        >重置</el-button>
      </div>
      <div class="rm-toolbar__group">
        <span class="toolbar-summary text-mono">
          显示 {{ filteredTasks.length }} / {{ tasks.length }} 个任务
        </span>
      </div>
    </div>

    <!-- 任务列表 -->
    <div class="rm-surface" v-loading="loading">
      <div v-if="!loading && filteredTasks.length === 0" class="empty">
        <div class="empty__icon"><i class="el-icon-tickets"></i></div>
        <div class="empty__title">{{ filterActiveCount > 0 ? '没有符合条件的任务' : '还没有任何备份任务' }}</div>
        <div class="empty__desc">{{ filterActiveCount > 0 ? '试试调整筛选条件，或创建新任务' : '创建第一个备份任务开始保护你的数据' }}</div>
        <div class="empty__actions">
          <el-button v-if="filterActiveCount > 0" size="small" plain @click="resetFilters">清空筛选</el-button>
          <el-button type="primary" size="small" round icon="el-icon-plus" @click="openCreateDialog">
            {{ filterActiveCount > 0 ? '新建任务' : '创建第一个任务' }}
          </el-button>
        </div>
      </div>

      <el-table v-else :data="filteredTasks" stripe style="width: 100%">
        <el-table-column label="任务名称与源路径" min-width="250">
          <template #default="scope">
            <div class="task-row">
              <div class="task-row__icon" :class="taskIconClass(scope.row)">
                <i :class="isTaskRunning(scope.row) ? 'el-icon-loading' : 'el-icon-tickets'"></i>
              </div>
              <div class="task-row__body">
                <div class="task-row__name" :title="scope.row.name">{{ scope.row.name }}</div>
                <div class="task-row__path">
                  <span class="rm-code-chip" :title="scope.row.sourcePath">
                    <span class="rm-code-chip__text">{{ truncatePath(scope.row.sourcePath) }}</span>
                  </span>
                </div>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="仓库与规则" min-width="160">
          <template #default="scope">
            <div class="repo-rule-cell">
              <div class="repo-rule-cell__repo">
                <i class="el-icon-box"></i>
                <span>{{ getRepositoryName(scope.row.repositoryId) }}</span>
              </div>
              <div class="repo-rule-cell__rule">
                <i class="el-icon-collection-tag"></i>
                <span v-if="getBackupRuleName(scope.row.backupRuleId)">{{ getBackupRuleName(scope.row.backupRuleId) }}</span>
                <span v-else class="text-muted">未绑定规则</span>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="调度计划" min-width="190">
          <template #default="scope">
            <div v-if="scope.row.cronExpression" class="cron-cell">
              <div class="cron-cell__human">{{ describeCron(scope.row.cronExpression) }}</div>
              <span class="rm-code-chip cron-cell__expr">{{ scope.row.cronExpression }}</span>
            </div>
            <span v-else class="rm-status-pill rm-status-pill--neutral">
              <span class="rm-status-pill__dot"></span>
              手动触发
            </span>
          </template>
        </el-table-column>

        <el-table-column label="状态" width="148">
          <template #default="scope">
            <div class="status-cell">
              <span class="rm-status-pill" :class="getTaskStatusPillClass(scope.row)">
                <span class="rm-status-pill__dot"></span>
                {{ getTaskStatusLabel(scope.row) }}
              </span>
              <span v-if="!scope.row.isEnabled" class="paused-hint">定时已暂停</span>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="上次运行" width="166">
          <template #default="scope">
            <div v-if="scope.row.lastRunAt" class="text-mono text-secondary" style="font-size: 12px;">
              {{ formatDate(scope.row.lastRunAt) }}
            </div>
            <span v-else class="text-muted">从未执行</span>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="290" fixed="right" align="right">
          <template #default="scope">
            <div class="row-actions">
              <el-button
                size="mini"
                type="primary"
                plain
                :loading="isTaskRunning(scope.row)"
                icon="el-icon-video-play"
                @click="runTask(scope.row)"
              >立即运行</el-button>
              <el-button size="mini" type="text" icon="el-icon-document" @click="viewRecords(scope.row)">历史</el-button>
              <el-button size="mini" type="text" icon="el-icon-folder-opened" @click="browseFiles(scope.row)">预览</el-button>
              <el-button size="mini" type="text" icon="el-icon-edit" @click="openEditDialog(scope.row)">编辑</el-button>
              <el-button size="mini" type="text" class="text-danger" icon="el-icon-delete" @click="deleteTask(scope.row)">删除</el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 任务编辑对话框 -->
    <el-dialog
      :title="editingTask ? '编辑备份任务' : '新建备份任务'"
      :visible.sync="dialogVisible"
      width="680px"
      :close-on-click-modal="false"
      append-to-body
    >
      <el-form :model="form" :rules="rules" ref="formRef" label-position="top">
        <!-- 分区 1：基础信息 -->
        <div class="form-section">
          <div class="form-section__title">
            <i class="el-icon-tickets"></i> 基础信息
          </div>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="任务名称" prop="name">
                <el-input v-model="form.name" placeholder="为任务起一个易记的名字"></el-input>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="目标仓库" prop="repositoryId">
                <el-select v-model="form.repositoryId" placeholder="选择目标仓库" class="w-100">
                  <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id"></el-option>
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="启用状态" class="mb-0">
            <el-switch v-model="form.isEnabled" active-text="启用自动调度" inactive-text="暂停调度"></el-switch>
          </el-form-item>
        </div>

        <!-- 分区 2：备份路径与规则 -->
        <div class="form-section">
          <div class="form-section__title">
            <i class="el-icon-folder-opened"></i> 备份路径与规则
          </div>
          <el-form-item label="源路径" prop="sourcePath">
            <folder-selector v-model="form.sourcePath" title="选择源路径" placeholder="选择或输入要备份的目录"></folder-selector>
          </el-form-item>
          <el-form-item label="备份规则（可选）" class="mb-0">
            <el-select v-model="form.backupRuleId" placeholder="不绑定规则（备份全部文件）" clearable class="w-100">
              <el-option v-for="rule in backupRules" :key="rule.id" :label="rule.name" :value="rule.id"></el-option>
            </el-select>
            <div class="form-tip"><i class="el-icon-info"></i> 绑定规则后，将自动应用对应的包含/排除模式与快照保留策略</div>
          </el-form-item>
        </div>

        <!-- 分区 3：定时计划 -->
        <div class="form-section form-section--last">
          <div class="form-section__title">
            <i class="el-icon-time"></i> 定时计划（6 段 Quartz Cron）
          </div>
          <el-form-item label="Cron 表达式" class="mb-0">
            <el-input
              v-model="form.cronExpression"
              placeholder="例如: 0 0 0 * * ?（留空表示仅手动执行）"
              class="text-mono"
            ></el-input>
            <div v-if="form.cronExpression && form.cronExpression.trim()" class="cron-preview">
              <i class="el-icon-alarm-clock"></i>
              解析预览：<strong>{{ describeCron(form.cronExpression) }}</strong>
            </div>
            <div class="cron-presets">
              <span class="cron-presets__label">常用快捷：</span>
              <button type="button" class="cron-preset-btn" @click="form.cronExpression = '0 0 0 * * ?'">每天 00:00</button>
              <button type="button" class="cron-preset-btn" @click="form.cronExpression = '0 0 2 * * ?'">每天 02:00</button>
              <button type="button" class="cron-preset-btn" @click="form.cronExpression = '0 0 */6 * * ?'">每 6 小时</button>
              <button type="button" class="cron-preset-btn" @click="form.cronExpression = '0 0 0 ? * SUN'">每周日 00:00</button>
              <button type="button" class="cron-preset-btn" @click="form.cronExpression = ''">清空（手动）</button>
            </div>
          </el-form-item>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveTask" :loading="saving">{{ editingTask ? '保存修改' : '创建任务' }}</el-button>
      </template>
    </el-dialog>

    <!-- 备份记录抽屉 -->
    <el-drawer :title="`${currentTaskName} · 备份记录`" :visible.sync="drawerVisible" direction="rtl" size="640px" append-to-body>
      <div class="drawer-header-toolbar">
        <span class="text-secondary text-mono" style="font-size: 12px;">共 {{ drawerTotal }} 条记录</span>
        <el-button size="mini" type="text" icon="el-icon-top-right" @click="$router.push(`/records?taskId=${currentTaskId}`)">
          在完整列表查看
        </el-button>
      </div>
      <div class="drawer-content" v-loading="drawerLoading">
        <div v-if="drawerRecords.length === 0 && !drawerLoading" class="empty empty--small">
          <div class="empty__title">该任务暂无执行记录</div>
          <div class="empty__desc">点击「立即运行」按钮即可触发一次备份</div>
        </div>
        <div v-for="record in drawerRecords" :key="record.id" class="record-item" :class="{ 'record-item--failed': record.status === 'Failed' }">
          <div class="record-header">
            <span class="rm-status-pill" :class="getRecordPillClass(record.status)">
              <span class="rm-status-pill__dot"></span>
              {{ getStatusText(record.status) }}
            </span>
            <span class="record-time text-mono">{{ formatDate(record.startTime) }}</span>
          </div>
          <div class="record-details">
            <div class="detail-grid">
              <div class="detail-item">
                <span class="detail-label">耗时</span>
                <span class="text-mono">{{ calculateDuration(record.startTime, record.endTime) }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">处理文件</span>
                <span class="text-bold text-mono">{{ record.filesProcessed || 0 }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">传输数据</span>
                <span class="text-mono">{{ formatBytes(record.dataTransferred) }}</span>
              </div>
              <div class="detail-item" v-if="record.summary">
                <span class="detail-label">新增文件</span>
                <span class="text-success text-bold text-mono">{{ record.summary.files_new || 0 }}</span>
              </div>
              <div class="detail-item" v-if="record.summary">
                <span class="detail-label">修改文件</span>
                <span class="text-warning text-bold text-mono">{{ record.summary.files_changed || 0 }}</span>
              </div>
              <div class="detail-item" v-if="record.summary">
                <span class="detail-label">未变文件</span>
                <span class="text-secondary text-mono">{{ record.summary.files_unmodified || 0 }}</span>
              </div>
            </div>
            <div v-if="record.error" class="error-section">
              <div class="detail-label text-danger"><i class="el-icon-warning"></i> 错误信息</div>
              <div class="error-content">{{ record.error }}</div>
            </div>
            <div class="record-actions" v-if="record.output">
              <el-button size="mini" type="text" icon="el-icon-tickets" @click="showLog(record)">查看完整日志</el-button>
            </div>
          </div>
        </div>

        <!-- 抽屉分页 -->
        <div class="drawer-pagination" v-if="drawerTotal > drawerPageSize">
          <el-pagination
            small
            background
            layout="total, prev, pager, next"
            :current-page="drawerPage"
            :page-size="drawerPageSize"
            :total="drawerTotal"
            @current-change="handleDrawerPageChange"
          />
        </div>
      </div>
    </el-drawer>

    <!-- 文件浏览抽屉 -->
    <el-drawer :title="`${currentTaskName} · 文件结构预览`" :visible.sync="fileDrawerVisible" direction="rtl" size="800px" append-to-body>
      <div class="drawer-content">
        <el-alert v-if="currentExcludePatterns" :title="'当前排除规则: ' + currentExcludePatterns" type="info" show-icon :closable="false" style="margin-bottom: 12px;"></el-alert>
        <div class="file-browser" v-loading="fileTreeLoading">
          <el-tree
            v-if="fileDrawerVisible && fileTreeReady"
            ref="fileTree"
            :key="currentTaskId + ':' + fileTreeVersion"
            :props="fileTreeProps"
            :load="loadTreeNode"
            lazy
            node-key="path"
            :expand-on-click-node="false"
            :highlight-current="true"
          >
            <span class="custom-tree-node" slot-scope="{ node, data }">
              <span :class="{ 'excluded-file': data.isExcluded }">
                <i :class="data.icon"></i> {{ node.label }}
              </span>
            </span>
          </el-tree>
          <div v-if="fileTreeReady && !fileTreeLoading && fileTreeData.length === 0" class="empty empty--small">
            <div class="empty__title">该目录下没有文件</div>
          </div>
        </div>
      </div>
    </el-drawer>

    <!-- 日志弹窗 -->
    <el-dialog title="备份日志" :visible.sync="logDialogVisible" width="720px" append-to-body>
      <div class="rm-terminal">
        <div class="rm-terminal__header">
          <span><i class="el-icon-tickets"></i> restic output</span>
        </div>
        <div class="rm-terminal__body">{{ currentLog || '（无输出内容）' }}</div>
      </div>
      <template #footer>
        <el-button @click="logDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui';
import { backupService } from '@/services/backup.service';
import { backupRecordService } from '@/services/backup-record.service';
import { backupRuleService } from '@/services/backup-rule.service';
import { repositoryService } from '@/services/repository.service';
import FolderSelector from '@/components/FolderSelector.vue';

const WEEKDAY_MAP = {
  SUN: '周日',
  MON: '周一',
  TUE: '周二',
  WED: '周三',
  THU: '周四',
  FRI: '周五',
  SAT: '周六',
  '1': '周日',
  '2': '周一',
  '3': '周二',
  '4': '周三',
  '5': '周四',
  '6': '周五',
  '7': '周六'
};

export default {
  name: 'BackupTasks',
  components: { FolderSelector },
  data() {
    return {
      loading: false,
      saving: false,
      tasks: [],
      repositories: [],
      backupRules: [],
      dialogVisible: false,
      drawerVisible: false,
      logDialogVisible: false,
      editingTask: null,
      currentTaskId: null,
      currentTaskName: '',
      records: [],
      drawerRecords: [],
      drawerTotal: 0,
      drawerPage: 1,
      drawerPageSize: 5,
      drawerLoading: false,
      currentLog: '',
      form: {
        name: '',
        sourcePath: '',
        repositoryId: null,
        backupRuleId: null,
        cronExpression: '',
        isEnabled: true
      },
      filters: {
        name: '',
        state: '',
        repositoryId: 0,
        ruleId: 0
      },
      rules: {
        name: [{ required: true, message: '请输入任务名称', trigger: 'blur' }],
        sourcePath: [{ required: true, message: '请选择源路径', trigger: 'blur' }],
        repositoryId: [{ required: true, message: '请选择仓库', trigger: 'change' }]
      },
      fileDrawerVisible: false,
      fileTreeData: [],
      fileTreeVersion: 0,
      fileTreeReady: false,
      fileTreeLoading: false,
      browseReqSeq: 0,
      drawerReqSeq: 0,
      fileTreeProps: {
        label: 'name',
        children: 'children',
        isLeaf: 'isLeaf'
      },
      currentExcludePatterns: null,
      currentSourcePath: ''
    };
  },
  computed: {
    repoMap() {
      const map = new Map();
      this.repositories.forEach(r => map.set(r.id, r));
      return map;
    },
    ruleMap() {
      const map = new Map();
      this.backupRules.forEach(r => map.set(r.id, r));
      return map;
    },
    // 正在后台执行备份的任务 ID 集合（来自全局任务轮询，驱动"运行"按钮的 loading）
    activeRunIds() {
      return this.$store.getters['tasks/activeBackupRunTargetKeys'].map(Number);
    },
    runningCount() {
      return this.tasks.filter(t => this.isTaskRunning(t)).length;
    },
    enabledCount() {
      return this.tasks.filter(t => t.isEnabled).length;
    },
    scheduledCount() {
      return this.tasks.filter(t => t.cronExpression && String(t.cronExpression).trim()).length;
    },
    lastFailedCount() {
      return this.tasks.filter(t => t.lastRunStatus === 'Failed').length;
    },
    filteredTasks() {
      let r = this.tasks;
      const kw = (this.filters.name || '').trim().toLowerCase();
      if (kw) {
        r = r.filter(t => {
          const name = (t.name || '').toLowerCase();
          const path = (t.sourcePath || '').toLowerCase();
          return name.includes(kw) || path.includes(kw);
        });
      }
      if (this.filters.repositoryId) {
        r = r.filter(t => t.repositoryId === this.filters.repositoryId);
      }
      if (this.filters.ruleId) {
        r = r.filter(t => t.backupRuleId === this.filters.ruleId);
      }
      if (this.filters.state === 'enabled') {
        r = r.filter(t => !!t.isEnabled);
      } else if (this.filters.state === 'disabled') {
        r = r.filter(t => !t.isEnabled);
      } else if (this.filters.state === 'running') {
        r = r.filter(t => this.isTaskRunning(t));
      } else if (this.filters.state === 'failed') {
        r = r.filter(t => t.lastRunStatus === 'Failed');
      }
      return r;
    },
    filterActiveCount() {
      let n = 0;
      if (this.filters.name && this.filters.name.trim()) n++;
      if (this.filters.state) n++;
      if (this.filters.repositoryId) n++;
      if (this.filters.ruleId) n++;
      return n;
    }
  },
  watch: {
    // 全局轮询发现某个 BackupRun 任务结束（从活动列表消失）时自动刷新列表，
    // 让"上次结果"徽章及时从 运行中 翻转到 成功/失败
    activeRunIds(newVal, oldVal) {
      if (!oldVal) return;
      if (oldVal.some(id => !newVal.includes(id))) {
        this.fetchTasks().catch(() => {});
      }
    }
  },
  async mounted() {
    await this.refresh();
    // 处理 ?action=new 参数
    if (this.$route.query.action === 'new') {
      this.$nextTick(() => this.openCreateDialog());
    }
  },
  methods: {
    isTaskRunning(task) {
      return this.activeRunIds.includes(task.id) || task.lastRunStatus === 'Running';
    },
    taskIconClass(task) {
      if (this.isTaskRunning(task)) return 'task-row__icon--running';
      if (task.lastRunStatus === 'Failed') return 'task-row__icon--danger';
      if (!task.isEnabled) return 'task-row__icon--neutral';
      if (task.lastRunStatus === 'Succeeded') return 'task-row__icon--success';
      return 'task-row__icon--brand';
    },
    getTaskStatusPillClass(task) {
      if (this.isTaskRunning(task)) return 'rm-status-pill--running';
      if (task.lastRunStatus === 'Failed') return 'rm-status-pill--danger';
      if (task.lastRunStatus === 'Succeeded') return task.isEnabled ? 'rm-status-pill--success' : 'rm-status-pill--neutral';
      if (!task.isEnabled) return 'rm-status-pill--neutral';
      return 'rm-status-pill--info';
    },
    getTaskStatusLabel(task) {
      if (this.isTaskRunning(task)) return '运行中';
      if (task.lastRunStatus === 'Failed') return '上次失败';
      if (task.lastRunStatus === 'Succeeded') return task.isEnabled ? '正常' : '已暂停';
      if (!task.isEnabled) return '已暂停';
      return '待执行';
    },
    getRecordPillClass(status) {
      if (status === 'Running') return 'rm-status-pill--running';
      if (status === 'Succeeded') return 'rm-status-pill--success';
      if (status === 'Failed') return 'rm-status-pill--danger';
      if (status === 'Pending') return 'rm-status-pill--info';
      return 'rm-status-pill--neutral';
    },
    describeCron(expr) {
      if (!expr || !String(expr).trim()) return '手动触发';
      const parts = String(expr).trim().split(/\s+/);
      if (parts.length < 6) return '自定义计划';
      const sec = parts[0];
      const min = parts[1];
      const hour = parts[2];
      const dom = parts[3];
      const month = parts[4];
      const dow = parts[5];

      const pad2 = v => String(v).padStart(2, '0');
      const isNum = v => /^\d+$/.test(v);

      // 每 N 分钟
      if (sec === '0' && /^\*\/(\d+)$/.test(min) && hour === '*' && (dom === '*' || dom === '?') && month === '*') {
        return `每 ${min.slice(2)} 分钟`;
      }
      // 每 N 小时
      if (sec === '0' && isNum(min) && /^\*\/(\d+)$/.test(hour) && (dom === '*' || dom === '?') && month === '*') {
        return min === '0' ? `每 ${hour.slice(2)} 小时` : `每 ${hour.slice(2)} 小时第 ${min} 分`;
      }
      // 每小时
      if (sec === '0' && isNum(min) && hour === '*' && (dom === '*' || dom === '?') && month === '*') {
        return min === '0' ? '每小时整点' : `每小时第 ${min} 分`;
      }
      // 每天 HH:mm
      if (sec === '0' && isNum(min) && isNum(hour) && dom === '*' && month === '*' && (dow === '?' || dow === '*')) {
        return `每天 ${pad2(hour)}:${pad2(min)}`;
      }
      // 每周某天 HH:mm
      if (sec === '0' && isNum(min) && isNum(hour) && (dom === '?' || dom === '*') && month === '*' && dow !== '?' && dow !== '*') {
        const dayText = dow.split(',').map(d => WEEKDAY_MAP[d.toUpperCase()] || d).join('、');
        return `每${dayText} ${pad2(hour)}:${pad2(min)}`;
      }
      // 每月 N 日 HH:mm
      if (sec === '0' && isNum(min) && isNum(hour) && isNum(dom) && month === '*' && (dow === '?' || dow === '*')) {
        return `每月 ${dom} 日 ${pad2(hour)}:${pad2(min)}`;
      }
      return '自定义定时计划';
    },
    async refresh() {
      this.loading = true;
      try {
        await Promise.all([this.fetchTasks(), this.fetchRepositories(), this.fetchBackupRules()]);
      } finally {
        this.loading = false;
      }
    },
    async fetchTasks() {
      this.tasks = await backupService.getTasks();
    },
    async fetchRepositories() {
      this.repositories = await repositoryService.getRepositories();
    },
    async fetchBackupRules() {
      this.backupRules = await backupRuleService.getAllRules();
    },
    openCreateDialog() {
      this.editingTask = null;
      this.form = { name: '', sourcePath: '', repositoryId: null, backupRuleId: null, cronExpression: '', isEnabled: true };
      this.dialogVisible = true;
      this.$nextTick(() => { this.$refs.formRef && this.$refs.formRef.clearValidate(); });
    },
    openEditDialog(task) {
      this.editingTask = task;
      this.form = { ...task };
      this.dialogVisible = true;
      this.$nextTick(() => { this.$refs.formRef && this.$refs.formRef.clearValidate(); });
    },
    async saveTask() {
      try {
        const valid = await this.$refs.formRef.validate();
        if (!valid) return;
      } catch (e) {
        return;
      }
      this.saving = true;
      try {
        if (this.editingTask) {
          await backupService.updateTask(this.editingTask.id, { ...this.form });
          Message.success('任务更新成功');
        } else {
          await backupService.createTask({ ...this.form });
          Message.success('任务创建成功');
        }
        this.dialogVisible = false;
        await this.fetchTasks();
      } catch (e) {
        Message.error(e.response?.data?.message || e.message);
      } finally {
        this.saving = false;
      }
    },
    async deleteTask(task) {
      try {
        await MessageBox.confirm(`确定要删除备份任务 "${task.name}" 吗？此操作不可恢复。`, '删除确认', {
          confirmButtonText: '确定删除',
          cancelButtonText: '取消',
          type: 'warning'
        });
      } catch {
        return;
      }
      try {
        await backupService.deleteTask(task.id);
        Message.success('任务已删除');
        await this.fetchTasks();
      } catch (e) {
        Message.error(e.response?.data?.message || e.message);
      }
    },
    async runTask(task) {
      let res;
      try {
        res = await backupService.runTask(task.id);
      } catch (e) {
        Message.error(e.response?.data?.message || e.message);
        return;
      }
      // 提交后台任务后立即返回；行运行态从全局任务状态派生（按钮转圈），
      // 完成通知由全局轮询弹出，列表由 activeRunIds watcher 自动刷新
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      Message.success('任务已提交，正在后台执行');
      await this.fetchTasks();
    },
    async viewRecords(task) {
      this.currentTaskId = task.id;
      this.currentTaskName = task.name;
      this.drawerPage = 1;
      this.drawerVisible = true;
      await this.fetchDrawerRecords();
    },
    async fetchDrawerRecords() {
      if (!this.currentTaskId) return;
      const reqSeq = ++this.drawerReqSeq;
      this.drawerLoading = true;
      try {
        const res = await backupRecordService.getRecordsByTaskIdPaged(this.currentTaskId, {
          page: this.drawerPage,
          pageSize: this.drawerPageSize
        });
        if (reqSeq !== this.drawerReqSeq) return;
        this.drawerRecords = res.items || [];
        this.drawerTotal = res.total || 0;
      } catch (e) {
        if (reqSeq !== this.drawerReqSeq) return;
        Message.error('加载执行记录失败: ' + (e.response?.data?.message || e.message));
      } finally {
        if (reqSeq === this.drawerReqSeq) {
          this.drawerLoading = false;
        }
      }
    },
    handleDrawerPageChange(page) {
      this.drawerPage = page;
      this.fetchDrawerRecords();
    },
    showLog(record) {
      this.currentLog = record.output;
      this.logDialogVisible = true;
    },
    handleFilterChange() { /* computed */ },
    resetFilters() {
      this.filters = { name: '', state: '', repositoryId: 0, ruleId: 0 };
    },
    calculateDuration(s, e) {
      if (!s) return '-';
      if (!e) return '运行中';
      const ms = new Date(e) - new Date(s);
      const sec = Math.max(0, Math.floor(ms / 1000));
      const m = Math.floor(sec / 60);
      const h = Math.floor(m / 60);
      if (h) return `${h}h${m % 60}m${sec % 60}s`;
      if (m) return `${m}m${sec % 60}s`;
      return `${sec}s`;
    },
    formatBytes(b) {
      if (!b) return '0 B';
      const k = 1024;
      const units = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(b) / Math.log(k));
      return parseFloat((b / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
    },
    getRepositoryName(id) {
      const r = this.repoMap.get(id);
      return r ? r.name : '未知仓库';
    },
    getBackupRuleName(id) {
      if (!id) return '';
      const r = this.ruleMap.get(id);
      return r ? r.name : '';
    },
    formatDate(d) {
      if (!d) return '';
      try {
        return new Date(d).toLocaleString('zh-CN', { hour12: false });
      } catch {
        return '无效日期';
      }
    },
    truncatePath(p) {
      if (!p) return '';
      if (p.length <= 44) return p;
      return p.substring(0, 22) + '...' + p.substring(p.length - 19);
    },
    getStatusText(s) {
      const m = { Succeeded: '成功', Failed: '失败', Running: '运行中', NeverRun: '未运行', Pending: '排队中' };
      return m[s] || (s || '未运行');
    },
    async browseFiles(task) {
      const reqSeq = ++this.browseReqSeq;
      this.currentTaskId = task.id;
      this.currentTaskName = task.name;
      this.currentSourcePath = task.sourcePath;
      this.fileDrawerVisible = true;
      this.fileTreeReady = false;
      this.fileTreeLoading = true;
      this.currentExcludePatterns = '';
      this.fileTreeData = [];
      if (task.backupRuleId) {
        try {
          const rule = await backupRuleService.getRuleById(task.backupRuleId);
          if (reqSeq !== this.browseReqSeq) return;
          this.currentExcludePatterns = rule.excludePatterns;
        } catch (e) { /* 忽略规则加载错误，继续浏览 */ }
      }
      if (reqSeq !== this.browseReqSeq) return;
      this.fileTreeVersion += 1;
      this.fileTreeReady = true;
    },
    createTreeNode(item, parentPath) {
      let fullPath;
      if (!parentPath) fullPath = item.name;
      else if (parentPath.match(/[/\\]$/)) fullPath = parentPath + item.name;
      else {
        const sep = parentPath.includes('\\') ? '\\' : '/';
        fullPath = parentPath + sep + item.name;
      }
      const isExcluded = this.isFileExcluded(fullPath);
      let icon = 'el-icon-document';
      if (item.type === 'folder') icon = 'el-icon-folder';
      else if (item.type === 'drive') icon = 'el-icon-box';
      return {
        name: item.name,
        path: fullPath,
        type: item.type,
        icon,
        isLeaf: item.type === 'file',
        isExcluded
      };
    },
    async loadTreeNode(node, resolve) {
      const reqSeq = this.browseReqSeq;
      if (node.level === 0) {
        if (!this.currentSourcePath) {
          this.fileTreeData = [];
          this.fileTreeLoading = false;
          return resolve([]);
        }
        this.fileTreeLoading = true;
        try {
          const response = await backupService.getFolders(this.currentSourcePath);
          if (reqSeq !== this.browseReqSeq) return resolve([]);
          const nodes = Array.isArray(response)
            ? response.map(item => this.createTreeNode(item, this.currentSourcePath))
            : [];
          this.fileTreeData = nodes;
          resolve(nodes);
        } catch (error) {
          if (reqSeq === this.browseReqSeq) this.fileTreeData = [];
          resolve([]);
        } finally {
          if (reqSeq === this.browseReqSeq) this.fileTreeLoading = false;
        }
        return;
      }
      const path = node.data.path;
      try {
        const response = await backupService.getFolders(path);
        if (reqSeq !== this.browseReqSeq) return resolve([]);
        resolve(Array.isArray(response) ? response.map(item => this.createTreeNode(item, path)) : []);
      } catch (e) {
        if (reqSeq === this.browseReqSeq) {
          Message.error('加载子目录失败: ' + (e.response?.data?.message || e.message));
        }
        resolve([]);
      }
    },
    isFileExcluded(filePath) {
      if (!this.currentExcludePatterns) return false;
      try {
        const p = filePath.replace(/\\/g, '/');
        return this.currentExcludePatterns.split(';').some(pat => {
          if (!pat) return false;
          if (pat.startsWith('**/')) {
            const suffix = pat.substring(3);
            return p.endsWith('/' + suffix) || p.includes('/' + suffix);
          }
          if (pat.startsWith('*.')) return p.endsWith(pat.substring(1));
          if (pat.endsWith('/')) return p.startsWith(pat) || p.includes('/' + pat) || p === pat.slice(0, -1);
          return p.includes('/' + pat) || p.endsWith(pat) || p === pat;
        });
      } catch (e) {
        return false;
      }
    }
  }
};
</script>

<style scoped>
.tasks-page {
  padding-bottom: 24px;
}

.tasks-kpi-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.kpi-icon {
  font-size: 16px;
}
.kpi-icon--brand { color: var(--brand-400); }
.kpi-icon--success { color: var(--success-500); }
.kpi-icon--info { color: var(--info-500); }
.kpi-icon--danger { color: var(--danger-500); }

.toolbar-search {
  width: 240px;
  max-width: 100%;
}

.toolbar-select {
  width: 150px;
}

.toolbar-summary {
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 任务行 */
.task-row {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.task-row__icon {
  width: 36px;
  height: 36px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 16px;
}

.task-row__icon--brand { background: var(--brand-50); color: var(--brand-400); }
.task-row__icon--success { background: var(--success-50); color: var(--success-600); }
.task-row__icon--danger { background: var(--danger-50); color: var(--danger-600); }
.task-row__icon--running { background: var(--info-50); color: var(--info-600); }
.task-row__icon--neutral { background: var(--gray-100); color: var(--color-text-secondary); }

.task-row__body {
  min-width: 0;
  flex: 1;
}

.task-row__name {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 14px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-row__path {
  margin-top: 4px;
}

/* 仓库与规则 */
.repo-rule-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.repo-rule-cell__repo {
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 13px;
}

.repo-rule-cell__repo i {
  color: var(--brand-400);
}

.repo-rule-cell__rule {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 调度列 */
.cron-cell {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
}

.cron-cell__human {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.cron-cell__expr {
  font-size: 11px;
  color: var(--brand-400);
}

/* 状态列 */
.status-cell {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
}

.paused-hint {
  font-size: 11px;
  color: var(--color-text-placeholder);
}

/* 操作列 */
.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  white-space: nowrap;
}

/* 弹窗分组 */
.form-section {
  padding: 14px 16px;
  margin-bottom: 14px;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}

.form-section--last {
  margin-bottom: 0;
}

.form-section__title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 12px;
}

.form-section__title i {
  color: var(--brand-400);
}

.cron-preview {
  margin-top: 8px;
  padding: 6px 10px;
  border-radius: var(--radius-sm);
  background: var(--brand-50);
  border: 1px solid var(--brand-200);
  font-size: 12px;
  color: var(--brand-300);
  display: flex;
  align-items: center;
  gap: 6px;
}

.cron-presets {
  margin-top: 10px;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.cron-presets__label {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.cron-preset-btn {
  padding: 3px 10px;
  border-radius: var(--radius-full);
  border: 1px solid var(--color-border);
  background: var(--color-bg-card);
  color: var(--color-text-regular);
  font-size: 12px;
  cursor: pointer;
  transition: border-color var(--transition-fast), color var(--transition-fast), background-color var(--transition-fast);
}

.cron-preset-btn:hover {
  border-color: var(--brand-200);
  background: var(--brand-50);
  color: var(--brand-300);
}

.form-tip {
  margin-top: 6px;
  font-size: 12px;
  color: var(--color-text-secondary);
}

.form-tip i {
  margin-right: 4px;
}

.w-100 {
  width: 100%;
}

.mb-0 {
  margin-bottom: 0 !important;
}

/* 抽屉工具栏与分页 */
.drawer-header-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 18px;
  background: var(--color-bg-soft);
  border-bottom: 1px solid var(--color-border-soft);
}

.drawer-pagination {
  margin-top: 16px;
  padding: 12px 0;
  display: flex;
  justify-content: center;
  border-top: 1px solid var(--color-border-soft);
}

/* 空状态 */
.empty {
  text-align: center;
  padding: 64px 20px;
}

.empty--small {
  padding: 32px 20px;
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

.empty__actions {
  margin-top: 16px;
  display: inline-flex;
  gap: 10px;
}

/* 抽屉内容 */
.drawer-content {
  padding: 16px;
  height: 100%;
  overflow-y: auto;
}

.record-item {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  margin-bottom: 12px;
  background: var(--color-bg-card);
  overflow: hidden;
}

.record-item--failed {
  border-color: var(--danger-500);
}

.record-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  background: var(--color-bg-soft);
  border-bottom: 1px solid var(--color-border-soft);
}

.record-time {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.record-details {
  padding: 12px 14px;
}

.detail-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px 16px;
}

.detail-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 13px;
  padding: 4px 0;
}

.detail-label {
  color: var(--color-text-secondary);
  font-weight: 500;
}

.error-section {
  margin-top: 10px;
  padding: 10px;
  background: var(--danger-50);
  border-radius: var(--radius-sm);
  border-left: 3px solid var(--danger-500);
}

.error-content {
  margin-top: 6px;
  color: var(--danger-600);
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 12px;
  font-family: "JetBrains Mono", "SF Mono", Consolas, monospace;
}

.record-actions {
  margin-top: 10px;
  text-align: right;
  border-top: 1px dashed var(--color-border-soft);
  padding-top: 8px;
}

/* 文件浏览器 */
.file-browser {
  padding: 4px;
}

.custom-tree-node {
  flex: 1;
  display: flex;
  align-items: center;
  font-size: 14px;
  padding-right: 8px;
}

.excluded-file {
  color: var(--color-text-placeholder);
  text-decoration: line-through;
}

@media (max-width: 992px) {
  .tasks-kpi-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .toolbar-search,
  .toolbar-select {
    width: 100%;
  }
}
</style>