<template>
  <div class="tasks-page">
    <!-- 页面头 -->
    <div class="page-header">
      <div>
        <h2 class="page-header__title">备份任务</h2>
        <div class="page-header__subtitle">管理所有自动 / 手动备份任务及其执行计划</div>
      </div>
      <div class="page-header__actions">
        <el-button round :loading="loading" icon="el-icon-refresh" size="small" @click="refresh">刷新</el-button>
        <el-button type="primary" round icon="el-icon-plus" size="small" @click="openCreateDialog">新增任务</el-button>
      </div>
    </div>

    <!-- 概览统计 -->
    <el-row :gutter="16" class="mb-4">
      <el-col :xs="12" :sm="6">
        <div class="mini-stat mini-stat--brand">
          <div class="mini-stat__label">任务总数</div>
          <div class="mini-stat__value">{{ tasks.length }}</div>
        </div>
      </el-col>
      <el-col :xs="12" :sm="6">
        <div class="mini-stat mini-stat--success">
          <div class="mini-stat__label">已启用</div>
          <div class="mini-stat__value">{{ enabledCount }}</div>
        </div>
      </el-col>
      <el-col :xs="12" :sm="6">
        <div class="mini-stat mini-stat--warning">
          <div class="mini-stat__label">有计划</div>
          <div class="mini-stat__value">{{ scheduledCount }}</div>
        </div>
      </el-col>
      <el-col :xs="12" :sm="6">
        <div class="mini-stat mini-stat--danger">
          <div class="mini-stat__label">上次失败</div>
          <div class="mini-stat__value">{{ lastFailedCount }}</div>
        </div>
      </el-col>
    </el-row>

    <!-- 筛选条 -->
    <el-card class="filter-bar mb-4" shadow="never">
      <div class="filter-bar__row">
        <el-input
          v-model="filters.name"
          placeholder="按任务名称搜索..."
          prefix-icon="el-icon-search"
          clearable
          size="small"
          class="filter-bar__search"
          @input="handleFilterChange"
        />
        <el-select v-model="filters.repositoryId" placeholder="选择仓库" clearable size="small" @change="handleFilterChange" class="filter-bar__select">
          <el-option label="全部仓库" :value="0"></el-option>
          <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id"></el-option>
        </el-select>
        <el-select v-model="filters.ruleId" placeholder="选择规则" clearable size="small" @change="handleFilterChange" class="filter-bar__select">
          <el-option label="全部规则" :value="0"></el-option>
          <el-option v-for="r in backupRules" :key="r.id" :label="r.name" :value="r.id"></el-option>
        </el-select>
        <el-select v-model="filters.status" placeholder="状态" clearable size="small" @change="handleFilterChange" class="filter-bar__select">
          <el-option label="全部" value=""></el-option>
          <el-option label="已启用" value="enabled"></el-option>
          <el-option label="已禁用" value="disabled"></el-option>
        </el-select>
        <el-select v-model="filters.lastStatus" placeholder="上次结果" clearable size="small" @change="handleFilterChange" class="filter-bar__select">
          <el-option label="全部" value=""></el-option>
          <el-option label="成功" value="Succeeded"></el-option>
          <el-option label="失败" value="Failed"></el-option>
          <el-option label="从未执行" value="NeverRun"></el-option>
        </el-select>
      </div>
      <div v-if="filterActiveCount > 0" class="filter-bar__chips">
        <span class="text-secondary" style="font-size:12px; margin-right:4px;">已筛选:</span>
        <el-tag
          v-for="chip in activeChips"
          :key="chip.key"
          closable
          size="mini"
          @close="clearFilter(chip.key)"
          effect="plain"
        >
          {{ chip.label }}
        </el-tag>
        <el-button type="text" size="mini" @click="resetFilters">清除全部</el-button>
      </div>
    </el-card>

    <!-- 任务列表 -->
    <el-card shadow="never">
      <div v-if="!loading && filteredTasks.length === 0" class="empty">
        <div class="empty__title">{{ filterActiveCount > 0 ? '没有符合条件的任务' : '还没有任何备份任务' }}</div>
        <div class="empty__desc">{{ filterActiveCount > 0 ? '试试调整筛选条件,或创建新任务' : '创建第一个备份任务开始保护你的数据' }}</div>
        <el-button type="primary" round icon="el-icon-plus" @click="openCreateDialog" class="mt-3">
          {{ filterActiveCount > 0 ? '创建新任务' : '创建第一个任务' }}
        </el-button>
      </div>

      <el-table v-else :data="filteredTasks" v-loading="loading" stripe style="width:100%">
        <el-table-column label="任务" min-width="220">
          <template #default="scope">
            <div class="task-row">
              <div class="task-row__icon">
                <i class="el-icon-tickets"></i>
              </div>
              <div class="task-row__body">
                <div class="task-row__name">{{ scope.row.name }}</div>
                <div class="task-row__meta">
                  <span class="rm-badge" :class="scope.row.isEnabled ? 'rm-badge--success' : 'rm-badge--neutral'">
                    {{ scope.row.isEnabled ? '已启用' : '已禁用' }}
                  </span>
                  <span class="rm-badge" :class="`rm-badge--${getStatusTone(scope.row.lastRunStatus)}`">
                    {{ getStatusText(scope.row.lastRunStatus) }}
                  </span>
                </div>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="源路径" min-width="200">
          <template #default="scope">
            <div class="path-cell" :title="scope.row.sourcePath">
              <i class="el-icon-document path-cell__icon"></i>
              <span>{{ truncatePath(scope.row.sourcePath) }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="仓库" min-width="120">
          <template #default="scope">
            <span class="text-bold">{{ getRepositoryName(scope.row.repositoryId) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="规则" min-width="120">
          <template #default="scope">
            <span v-if="getBackupRuleName(scope.row.backupRuleId)" class="text-secondary">
              <i class="el-icon-collection-tag"></i> {{ getBackupRuleName(scope.row.backupRuleId) }}
            </span>
            <span v-else class="text-muted">未设置</span>
          </template>
        </el-table-column>
        <el-table-column label="计划" width="140">
          <template #default="scope">
            <code v-if="scope.row.cronExpression" class="cron-pill">{{ scope.row.cronExpression }}</code>
            <span v-else class="text-muted">手动</span>
          </template>
        </el-table-column>
        <el-table-column label="上次运行" width="170">
          <template #default="scope">
            <div v-if="scope.row.lastRunAt" class="text-mono" style="font-size:12px;">
              {{ formatDate(scope.row.lastRunAt) }}
            </div>
            <span v-else class="text-muted">从未</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="160" fixed="right" align="right">
          <template #default="scope">
            <div class="row-actions">
              <el-button size="mini" type="text" :loading="activeRunIds.includes(scope.row.id)" icon="el-icon-video-play" @click="runTask(scope.row)">运行</el-button>
              <el-dropdown trigger="click" size="medium" class="row-more" @command="(c) => handleRowCmd(c, scope.row)">
                <el-button size="mini" type="text" icon="el-icon-more"></el-button>
                <el-dropdown-menu slot="dropdown">
                  <el-dropdown-item icon="el-icon-document" command="records">查看记录</el-dropdown-item>
                  <el-dropdown-item icon="el-icon-folder" command="browse">浏览文件</el-dropdown-item>
                  <el-dropdown-item icon="el-icon-edit" command="edit">编辑任务</el-dropdown-item>
                  <el-dropdown-item icon="el-icon-delete" command="delete" divided>删除任务</el-dropdown-item>
                </el-dropdown-menu>
              </el-dropdown>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 任务编辑对话框 -->
    <el-dialog :title="editingTask ? '编辑备份任务' : '新建备份任务'" :visible.sync="dialogVisible" width="640px" :close-on-click-modal="false" append-to-body>
      <el-form :model="form" :rules="rules" ref="formRef" label-position="top">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="任务名称" prop="name">
              <el-input v-model="form.name" placeholder="为任务起一个易记的名字" size="default"></el-input>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="目标仓库" prop="repositoryId">
              <el-select v-model="form.repositoryId" placeholder="选择目标仓库" class="w-100" size="default">
                <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id"></el-option>
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item label="源路径" prop="sourcePath">
          <folder-selector v-model="form.sourcePath" title="选择源路径" placeholder="选择或输入要备份的目录"></folder-selector>
        </el-form-item>

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="备份规则">
              <el-select v-model="form.backupRuleId" placeholder="选择备份规则" clearable class="w-100" size="default">
                <el-option v-for="rule in backupRules" :key="rule.id" :label="rule.name" :value="rule.id"></el-option>
              </el-select>
              <div class="form-tip"><i class="el-icon-info"></i> 应用于该任务的备份规则 (可选)</div>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="启用任务">
              <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="停用"></el-switch>
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item label="计划 (Cron)">
          <el-input v-model="form.cronExpression" placeholder="例如: 0 0 * * * (每天凌晨执行)" size="default"></el-input>
          <div class="form-tip">
            <i class="el-icon-info"></i> 使用 Cron 表达式设置自动备份,留空表示手动执行。常用:
            <el-tag size="mini" effect="plain" class="cron-quick" @click="form.cronExpression = '0 0 * * *'">每天 0 点</el-tag>
            <el-tag size="mini" effect="plain" class="cron-quick" @click="form.cronExpression = '0 */6 * * *'">每 6 小时</el-tag>
            <el-tag size="mini" effect="plain" class="cron-quick" @click="form.cronExpression = '0 0 * * 0'">每周日</el-tag>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveTask" :loading="saving">{{ editingTask ? '保存修改' : '创建任务' }}</el-button>
      </template>
    </el-dialog>

    <!-- 备份记录抽屉 -->
    <el-drawer :title="`${currentTaskName} · 备份记录`" :visible.sync="drawerVisible" direction="rtl" size="640px" append-to-body>
      <div class="drawer-header-toolbar">
        <span class="text-secondary" style="font-size:12px;">共 {{ drawerTotal }} 条记录</span>
        <el-button size="mini" type="text" icon="el-icon-top-right" @click="$router.push(`/records?taskId=${currentTaskId}`)">
          在完整列表查看
        </el-button>
      </div>
      <div class="drawer-content" v-loading="drawerLoading">
        <div v-if="drawerRecords.length === 0 && !drawerLoading" class="empty empty--small">
          <div class="empty__title">该任务暂无执行记录</div>
          <div class="empty__desc">点击"运行"按钮执行一次备份</div>
        </div>
        <div v-for="record in drawerRecords" :key="record.id" class="record-item" :class="{ 'record-item--failed': record.status === 'Failed' }">
          <div class="record-header">
            <span class="rm-badge" :class="`rm-badge--${getStatusTone(record.status)}`">
              <span class="rm-badge__dot"></span>
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
                <span class="text-bold">{{ record.filesProcessed || 0 }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">传输数据</span>
                <span>{{ formatBytes(record.dataTransferred) }}</span>
              </div>
              <div class="detail-item" v-if="record.summary">
                <span class="detail-label">新增文件</span>
                <span class="text-success text-bold">{{ record.summary.files_new || 0 }}</span>
              </div>
              <div class="detail-item" v-if="record.summary">
                <span class="detail-label">修改文件</span>
                <span class="text-warning text-bold">{{ record.summary.files_changed || 0 }}</span>
              </div>
              <div class="detail-item" v-if="record.summary">
                <span class="detail-label">未变文件</span>
                <span class="text-secondary">{{ record.summary.files_unmodified || 0 }}</span>
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
    <el-drawer :title="`${currentTaskName} · 文件结构`" :visible.sync="fileDrawerVisible" direction="rtl" size="800px" append-to-body>
      <div class="drawer-content">
        <el-alert v-if="currentExcludePatterns" :title="'排除规则: ' + currentExcludePatterns" type="info" show-icon :closable="false" style="margin-bottom: 12px;"></el-alert>
        <div class="file-browser">
          <el-tree ref="fileTree" :data="fileTreeData" :props="fileTreeProps" :load="loadTreeNode" lazy node-key="path" :expand-on-click-node="false" :highlight-current="true">
            <span class="custom-tree-node" slot-scope="{ node, data }">
              <span :class="{ 'excluded-file': data.isExcluded }">
                <i :class="data.icon"></i> {{ node.label }}
              </span>
            </span>
          </el-tree>
          <div v-if="fileTreeData.length === 0" class="empty empty--small">
            <div class="empty__title">该目录下没有文件</div>
          </div>
        </div>
      </div>
    </el-drawer>

    <!-- 日志弹窗 -->
    <el-dialog title="备份日志" :visible.sync="logDialogVisible" width="640px" append-to-body>
      <pre class="log-content">{{ currentLog }}</pre>
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
        repositoryId: 0,
        ruleId: 0,
        status: '',
        lastStatus: ''
      },
      rules: {
        name: [{ required: true, message: '请输入任务名称', trigger: 'blur' }],
        sourcePath: [{ required: true, message: '请选择源路径', trigger: 'blur' }],
        repositoryId: [{ required: true, message: '请选择仓库', trigger: 'change' }]
      },
      fileDrawerVisible: false,
      fileTreeData: [],
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
    filteredTasks() {
      let r = this.tasks;
      if (this.filters.name) {
        const q = this.filters.name.toLowerCase();
        r = r.filter(t => t.name.toLowerCase().includes(q));
      }
      if (this.filters.repositoryId) r = r.filter(t => t.repositoryId === this.filters.repositoryId);
      if (this.filters.ruleId) r = r.filter(t => t.backupRuleId === this.filters.ruleId);
      if (this.filters.status) {
        const enabled = this.filters.status === 'enabled';
        r = r.filter(t => t.isEnabled === enabled);
      }
      if (this.filters.lastStatus) r = r.filter(t => t.lastRunStatus === this.filters.lastStatus);
      return r;
    },
    enabledCount() {
      return this.tasks.filter(t => t.isEnabled).length;
    },
    scheduledCount() {
      return this.tasks.filter(t => t.cronExpression).length;
    },
    lastFailedCount() {
      return this.tasks.filter(t => t.lastRunStatus === 'Failed').length;
    },
    filterActiveCount() {
      let n = 0;
      if (this.filters.name) n++;
      if (this.filters.repositoryId) n++;
      if (this.filters.ruleId) n++;
      if (this.filters.status) n++;
      if (this.filters.lastStatus) n++;
      return n;
    },
    activeChips() {
      const chips = [];
      if (this.filters.name) chips.push({ key: 'name', label: `名称: ${this.filters.name}` });
      if (this.filters.repositoryId) chips.push({ key: 'repositoryId', label: `仓库: ${this.getRepositoryName(this.filters.repositoryId)}` });
      if (this.filters.ruleId) chips.push({ key: 'ruleId', label: `规则: ${this.getBackupRuleName(this.filters.ruleId)}` });
      if (this.filters.status) chips.push({ key: 'status', label: this.filters.status === 'enabled' ? '已启用' : '已禁用' });
      if (this.filters.lastStatus) chips.push({ key: 'lastStatus', label: `上次: ${this.filters.lastStatus}` });
      return chips;
    },
    // 正在后台执行备份的任务 ID 集合（来自全局任务轮询，驱动"运行"按钮的 loading）
    activeRunIds() {
      return this.$store.getters['tasks/activeBackupRunTargetKeys'].map(Number);
    }
  },
  watch: {
    // 全局轮询发现某个 BackupRun 任务结束（从活动列表消失）时自动刷新列表，
    // 让"上次结果"徽章及时从 运行中 翻转到 成功/失败
    activeRunIds(newVal, oldVal) {
      if (!oldVal) return;
      if (newVal.length < oldVal.length) {
        this.fetchTasks();
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
    },
    openEditDialog(task) {
      this.editingTask = task;
      this.form = { ...task };
      this.dialogVisible = true;
    },
    async saveTask() {
      const valid = await this.$refs.formRef.validate();
      if (!valid) return;
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
      } catch { return; }
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
    async handleRowCmd(cmd, task) {
      switch (cmd) {
        case 'records': return this.viewRecords(task);
        case 'browse':  return this.browseFiles(task);
        case 'edit':    return this.openEditDialog(task);
        case 'delete':  return this.deleteTask(task);
      }
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
      this.drawerLoading = true;
      try {
        const res = await backupRecordService.getRecordsByTaskIdPaged(this.currentTaskId, {
          page: this.drawerPage,
          pageSize: this.drawerPageSize
        });
        this.drawerRecords = res.items || [];
        this.drawerTotal = res.total || 0;
      } catch (e) {
        Message.error('加载执行记录失败: ' + (e.response?.data?.message || e.message));
      } finally {
        this.drawerLoading = false;
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
    clearFilter(key) {
      if (key === 'name') this.filters.name = '';
      else if (key === 'repositoryId') this.filters.repositoryId = 0;
      else if (key === 'ruleId') this.filters.ruleId = 0;
      else if (key === 'status') this.filters.status = '';
      else if (key === 'lastStatus') this.filters.lastStatus = '';
    },
    resetFilters() {
      this.filters = { name: '', repositoryId: 0, ruleId: 0, status: '', lastStatus: '' };
    },
    calculateDuration(s, e) {
      if (!s) return '-';
      if (!e) return '运行中';
      const ms = new Date(e) - new Date(s);
      const sec = Math.floor(ms / 1000);
      const m = Math.floor(sec / 60);
      const h = Math.floor(m / 60);
      if (h) return `${h}h${m % 60}m${sec % 60}s`;
      if (m) return `${m}m${sec % 60}s`;
      return `${sec}s`;
    },
    formatBytes(b) {
      if (!b) return '0 B';
      const k = 1024, units = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(b) / Math.log(k));
      return parseFloat((b / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
    },
    getRepositoryName(id) {
      const r = this.repositories.find(x => x.id === id);
      return r ? r.name : '未知';
    },
    getBackupRuleName(id) {
      if (!id) return '';
      const r = this.backupRules.find(x => x.id === id);
      return r ? r.name : '';
    },
    formatDate(d) {
      if (!d) return '';
      try { return new Date(d).toLocaleString('zh-CN', { hour12: false }); }
      catch { return '无效日期'; }
    },
    truncatePath(p) {
      if (!p) return '';
      if (p.length <= 50) return p;
      return p.substring(0, 25) + '...' + p.substring(p.length - 22);
    },
    getStatusTone(s) {
      const m = { Succeeded: 'success', Failed: 'danger', Running: 'warning' };
      return m[s] || 'neutral';
    },
    getStatusText(s) {
      const m = { Succeeded: '成功', Failed: '失败', Running: '运行中', NeverRun: '未运行', Pending: '排队中' };
      return m[s] || (s || '未运行');
    },
    async browseFiles(task) {
      this.currentTaskId = task.id;
      this.currentTaskName = task.name;
      this.currentSourcePath = task.sourcePath;
      this.fileDrawerVisible = true;
      this.currentExcludePatterns = '';
      this.fileTreeData = [];
      if (task.backupRuleId) {
        try {
          const rule = await backupRuleService.getRuleById(task.backupRuleId);
          this.currentExcludePatterns = rule.excludePatterns;
        } catch (e) { /* 忽略规则加载错误,继续浏览 */ }
      }
      await this.loadRootFiles();
    },
    async loadRootFiles() {
      try {
        const response = await backupService.getFolders(this.currentSourcePath);
        this.fileTreeData = Array.isArray(response)
          ? response.map(item => this.createTreeNode(item, this.currentSourcePath))
          : [];
      } catch (error) {
        this.fileTreeData = [];
      }
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
      if (node.level === 0) return resolve(this.fileTreeData);
      const path = node.data.path;
      try {
        const response = await backupService.getFolders(path);
        resolve(response.map(item => this.createTreeNode(item, path)));
      } catch (e) {
        Message.error('加载子目录失败: ' + (e.response?.data?.message || e.message));
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
      } catch (e) { return false; }
    }
  }
};
</script>

<style scoped>
.tasks-page { padding-bottom: 24px; }

.mini-stat {
  padding: 14px 18px;
  border-radius: var(--radius-lg);
  background: var(--color-bg-card);
  border-left: 4px solid var(--brand-500);
  box-shadow: var(--shadow-sm);
  margin-bottom: 16px;
}
.mini-stat--success { border-left-color: var(--success-500); }
.mini-stat--warning { border-left-color: var(--warning-500); }
.mini-stat--danger  { border-left-color: var(--danger-500); }
.mini-stat__label { font-size: 12px; color: var(--color-text-secondary); }
.mini-stat__value { font-size: 22px; font-weight: 700; color: var(--color-text-primary); margin-top: 4px; }

/* 筛选条 */
.filter-bar { padding: 12px !important; }
.filter-bar__row {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  align-items: center;
}
.filter-bar__search { width: 240px; max-width: 100%; }
.filter-bar__select { width: 180px; }
.filter-bar__chips {
  margin-top: 10px;
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

/* 任务行 */
.task-row { display: flex; align-items: center; gap: 12px; }
.task-row__icon {
  width: 36px;
  height: 36px;
  border-radius: var(--radius-md);
  background: var(--brand-50);
  color: var(--brand-600);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 16px;
}
.task-row__body { min-width: 0; flex: 1; }
.task-row__name { font-weight: 600; color: var(--color-text-primary); }
.task-row__meta { display: flex; gap: 6px; margin-top: 4px; flex-wrap: wrap; }

.path-cell { display: inline-flex; align-items: center; gap: 6px; max-width: 100%; }
.path-cell__icon { color: var(--color-text-secondary); font-size: 12px; flex-shrink: 0; }
.path-cell span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 280px;
  display: inline-block;
  vertical-align: middle;
}

.cron-pill {
  display: inline-block;
  background: var(--color-bg-soft);
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  font-family: "SF Mono", Consolas, monospace;
  font-size: 12px;
  color: var(--brand-400);
  border: 1px solid var(--brand-100);
}

/* 操作列:统一 flex 布局防折行 */
.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  white-space: nowrap;
  gap: 6px;
}
.row-more { margin-left: 0; }

/* Cron 快捷 */
.cron-quick { margin-left: 6px; cursor: pointer; }

/* 抽屉工具栏与分页 */
.drawer-header-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
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

/* 表单 */
.form-tip { margin-top: 6px; font-size: 12px; color: var(--color-text-secondary); }
.form-tip i { margin-right: 4px; }
.w-100 { width: 100%; }

/* 空状态 */
.empty {
  text-align: center;
  padding: 60px 20px;
}
.empty--small { padding: 30px 20px; }
.empty__title { font-size: 16px; font-weight: 600; color: var(--color-text-primary); }
.empty__desc { font-size: 13px; color: var(--color-text-secondary); margin-top: 4px; }

/* 抽屉内容 */
.drawer-content { padding: 16px; height: 100%; overflow-y: auto; }

.record-item {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  margin-bottom: 12px;
  background: var(--color-bg-card);
  overflow: hidden;
}
.record-item--failed { border-color: rgba(239, 68, 68, 0.4); }
.record-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  background: var(--color-bg-soft);
  border-bottom: 1px solid var(--color-border-soft);
}
.record-time { font-size: 12px; color: var(--color-text-secondary); }

.record-details { padding: 12px 14px; }
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
.detail-label { color: var(--color-text-secondary); font-weight: 500; }

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
  font-family: "SF Mono", Consolas, monospace;
}

.record-actions {
  margin-top: 10px;
  text-align: right;
  border-top: 1px dashed var(--color-border-soft);
  padding-top: 8px;
}

/* 日志 */
.log-content {
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 500px;
  overflow-y: auto;
  background: var(--gray-900);
  color: #e2e8f0;
  padding: 14px;
  border-radius: var(--radius-md);
  font-family: "SF Mono", Consolas, monospace;
  font-size: 12px;
}

/* 文件浏览器 */
.file-browser { padding: 4px; }
.custom-tree-node {
  flex: 1;
  display: flex;
  align-items: center;
  font-size: 14px;
  padding-right: 8px;
}
.excluded-file { color: var(--gray-400); text-decoration: line-through; }

@media (max-width: 992px) {
  .filter-bar__search, .filter-bar__select { width: 100%; }
}
</style>