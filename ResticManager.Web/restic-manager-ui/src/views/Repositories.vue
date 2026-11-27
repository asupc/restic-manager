<template>
  <div class="repositories-page">
    <div class="page-header">
      <div>
        <h2 class="page-header__title">备份仓库</h2>
        <div class="page-header__subtitle">管理本地及远程备份仓库，所有备份任务最终写入此处</div>
      </div>
      <div class="page-header__actions">
        <el-button round icon="el-icon-refresh" size="small" :loading="loading" @click="fetchRepositories">刷新</el-button>
        <el-button type="primary" round icon="el-icon-plus" size="small" @click="openCreateDialog">新建仓库</el-button>
      </div>
    </div>

    <!-- 顶部紧凑统计条 -->
    <div class="rm-kpi-grid repo-kpi-grid">
      <div class="rm-kpi-card rm-kpi-card--brand">
        <div class="rm-kpi-card__label">
          <span>总仓库数</span>
          <i class="el-icon-coin repo-kpi-icon repo-kpi-icon--brand"></i>
        </div>
        <div class="rm-kpi-card__value">{{ repositories.length }}</div>
        <div class="rm-kpi-card__meta">覆盖 {{ distinctTypeCount }} 种存储后端</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--success">
        <div class="rm-kpi-card__label">
          <span>已初始化</span>
          <i class="el-icon-circle-check repo-kpi-icon repo-kpi-icon--success"></i>
        </div>
        <div class="rm-kpi-card__value">{{ initializedCount }}</div>
        <div class="rm-kpi-card__meta">可直接写入快照与执行维护</div>
      </div>
      <div class="rm-kpi-card rm-kpi-card--warning">
        <div class="rm-kpi-card__label">
          <span>未初始化</span>
          <i class="el-icon-warning-outline repo-kpi-icon repo-kpi-icon--warning"></i>
        </div>
        <div class="rm-kpi-card__value">{{ uninitializedCount }}</div>
        <div class="rm-kpi-card__meta">{{ uninitializedCount > 0 ? '建议尽快完成初始化或校验' : '全部仓库已就绪' }}</div>
      </div>
    </div>

    <!-- 统一筛选工具条 -->
    <div class="rm-toolbar">
      <div class="rm-toolbar__group">
        <el-input
          v-model="searchKeyword"
          placeholder="搜索仓库名称或路径..."
          prefix-icon="el-icon-search"
          clearable
          size="small"
          class="toolbar-search"
        />
        <el-select
          v-model="filterType"
          placeholder="仓库类型"
          clearable
          size="small"
          class="toolbar-select"
        >
          <el-option label="全部类型" value=""></el-option>
          <el-option v-for="t in repositoryTypes" :key="t" :label="typeMeta[t].label" :value="t">
            <i :class="typeMeta[t].icon" style="margin-right: 6px;"></i>{{ typeMeta[t].label }}
          </el-option>
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
        <span class="toolbar-summary text-mono">
          显示 {{ filteredRepositories.length }} / {{ repositories.length }} 个仓库
        </span>
      </div>
    </div>

    <!-- 仓库表格 -->
    <div class="rm-surface" v-loading="loading">
      <div v-if="!loading && filteredRepositories.length === 0" class="empty-state">
        <div class="empty-state__icon"><i class="el-icon-box"></i></div>
        <div class="empty-state__title">{{ hasActiveFilter ? '没有匹配的仓库' : '还没有配置备份仓库' }}</div>
        <div class="empty-state__desc">
          {{ hasActiveFilter ? '试试调整关键词或切换仓库类型筛选条件' : '创建一个本地目录或云端对象存储仓库，即可开始自动备份' }}
        </div>
        <div class="empty-state__actions">
          <el-button v-if="hasActiveFilter" size="small" plain @click="resetFilters">重置筛选</el-button>
          <el-button type="primary" size="small" round icon="el-icon-plus" @click="openCreateDialog">新建仓库</el-button>
        </div>
      </div>

      <el-table v-else :data="filteredRepositories" stripe style="width: 100%">
        <el-table-column label="仓库与路径" min-width="280">
          <template slot-scope="scope">
            <div class="repo-cell">
              <div class="repo-cell__icon" :class="typeBadgeClass(scope.row.type)">
                <i :class="typeIcon(scope.row.type)"></i>
              </div>
              <div class="repo-cell__body">
                <div class="repo-cell__top">
                  <span class="repo-cell__name" :title="scope.row.name">{{ scope.row.name }}</span>
                  <span class="repo-type-badge" :class="typeBadgeClass(scope.row.type)">
                    {{ typeLabel(scope.row.type) }}
                  </span>
                </div>
                <div class="repo-cell__path-wrap">
                  <span class="rm-code-chip repo-path-chip" :title="scope.row.path">
                    <span class="rm-code-chip__text">{{ scope.row.path }}</span>
                    <button
                      type="button"
                      class="rm-code-chip__copy"
                      title="复制仓库路径"
                      @click.stop="copyPath(scope.row.path)"
                    >
                      <i class="el-icon-document-copy"></i>
                    </button>
                  </span>
                </div>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="状态" width="160">
          <template slot-scope="scope">
            <div class="status-stack">
              <span
                class="rm-status-pill"
                :class="isRowBusy(scope.row) ? 'rm-status-pill--running' : (scope.row.isInitialized ? 'rm-status-pill--success' : 'rm-status-pill--warning')"
              >
                <span class="rm-status-pill__dot"></span>
                {{ isRowBusy(scope.row) ? getRowBusyText(scope.row) : (scope.row.isInitialized ? '已初始化' : '未初始化') }}
              </span>
              <span
                v-if="scope.row.healthStatus"
                class="rm-status-pill"
                :class="getHealthPillClass(scope.row.healthStatus)"
              >
                <span class="rm-status-pill__dot"></span>
                {{ getHealthText(scope.row.healthStatus) }}
              </span>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="快照数" width="96" align="right">
          <template slot-scope="scope">
            <span class="num-cell">
              {{ scope.row.stats && scope.row.stats.snapshots_count !== undefined && scope.row.stats.snapshots_count !== null ? scope.row.stats.snapshots_count : '-' }}
            </span>
          </template>
        </el-table-column>

        <el-table-column label="存储大小" width="118" align="right">
          <template slot-scope="scope">
            <span class="num-cell">
              {{ scope.row.stats ? formatFileSize(scope.row.stats.total_size) : '-' }}
            </span>
          </template>
        </el-table-column>

        <el-table-column label="文件数" width="104" align="right">
          <template slot-scope="scope">
            <span class="num-cell">
              {{ scope.row.stats && scope.row.stats.total_file_count !== undefined && scope.row.stats.total_file_count !== null ? scope.row.stats.total_file_count : '-' }}
            </span>
          </template>
        </el-table-column>

        <el-table-column label="创建时间" width="168">
          <template slot-scope="scope">
            <span class="text-mono text-secondary" style="font-size: 12px;">{{ formatDate(scope.row.createdAt) }}</span>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="210" fixed="right" align="right">
          <template slot-scope="scope">
            <div class="row-actions">
              <el-button
                v-if="scope.row.isInitialized"
                size="mini"
                type="primary"
                plain
                icon="el-icon-camera"
                @click="viewSnapshots(scope.row)"
              >快照</el-button>
              <el-button
                v-else
                size="mini"
                type="primary"
                plain
                icon="el-icon-finished"
                :loading="!!scope.row.initializing"
                @click="initializeRepository(scope.row)"
              >初始化</el-button>

              <el-dropdown
                trigger="click"
                size="medium"
                @command="(cmd) => handleRowAction(cmd, scope.row)"
              >
                <el-button
                  size="mini"
                  :loading="!!(scope.row.maintenanceLoading || scope.row.loadingStats || scope.row.validating)"
                >
                  更多运维 <i class="el-icon-arrow-down el-icon--right"></i>
                </el-button>
                <el-dropdown-menu slot="dropdown">
                  <el-dropdown-item command="validate" icon="el-icon-circle-check" :disabled="!!scope.row.validating">
                    {{ scope.row.isInitialized ? '快速校验' : '验证仓库结构' }}
                  </el-dropdown-item>
                  <template v-if="scope.row.isInitialized">
                    <el-dropdown-item command="stats" icon="el-icon-data-analysis" :disabled="!!scope.row.loadingStats">
                      刷新统计
                    </el-dropdown-item>
                    <el-dropdown-item command="deep-check" icon="el-icon-search" :disabled="!!scope.row.maintenanceLoading">
                      深度校验 (5%)
                    </el-dropdown-item>
                    <el-dropdown-item command="prune" icon="el-icon-brush" :disabled="!!scope.row.maintenanceLoading">
                      清理未引用数据 (prune)
                    </el-dropdown-item>
                    <el-dropdown-item command="unlock" icon="el-icon-unlock" :disabled="!!scope.row.maintenanceLoading" divided>
                      解锁陈旧锁
                    </el-dropdown-item>
                    <el-dropdown-item command="force-unlock" icon="el-icon-key" :disabled="!!scope.row.maintenanceLoading">
                      强制解锁全部锁
                    </el-dropdown-item>
                    <el-dropdown-item command="repair-index" icon="el-icon-s-tools" :disabled="!!scope.row.maintenanceLoading">
                      修复索引
                    </el-dropdown-item>
                    <el-dropdown-item command="repair-snapshots" icon="el-icon-first-aid-kit" :disabled="!!scope.row.maintenanceLoading">
                      修复损坏快照
                    </el-dropdown-item>
                  </template>
                  <el-dropdown-item command="edit" icon="el-icon-edit" divided>编辑仓库</el-dropdown-item>
                  <el-dropdown-item command="delete" icon="el-icon-delete" class="dropdown-item-danger">删除仓库</el-dropdown-item>
                </el-dropdown-menu>
              </el-dropdown>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 创建/编辑对话框 -->
    <el-dialog
      :visible.sync="dialogVisible"
      :title="editingRepository ? '编辑仓库' : '新建仓库'"
      width="680px"
      :close-on-click-modal="false"
      append-to-body
    >
      <el-form :model="form" :rules="rules" ref="formRef" label-position="top">
        <!-- 分区 1：基础信息 -->
        <div class="form-section">
          <div class="form-section__title">
            <i class="el-icon-collection-tag"></i> 基础信息
          </div>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="仓库名称" prop="name">
                <el-input v-model="form.name" placeholder="例如: 主服务器备份"></el-input>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="仓库类型" prop="type">
                <el-select v-model="form.type" style="width: 100%" :disabled="!!editingRepository" @change="handleTypeChange">
                  <el-option v-for="t in repositoryTypes" :key="t" :label="typeMeta[t].label" :value="t">
                    <i :class="typeMeta[t].icon" style="margin-right: 8px;"></i>{{ typeMeta[t].label }}
                  </el-option>
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <div class="form-tip form-tip--inline"><i class="el-icon-info"></i> {{ currentMeta.desc }}</div>
        </div>

        <!-- 分区 2：存储位置 -->
        <div class="form-section">
          <div class="form-section__title">
            <i class="el-icon-folder-opened"></i> 存储位置配置
          </div>

          <!-- 本地目录：服务端目录浏览器选择路径 -->
          <el-form-item
            v-if="form.type === 'local'"
            label="仓库路径"
            prop="locationFields.path"
            :rules="[{ required: true, message: '请输入仓库路径', trigger: 'blur' }]"
          >
            <folder-selector v-model="form.locationFields.path" title="选择仓库路径" placeholder="选择或输入仓库存储路径"></folder-selector>
            <div class="form-tip"><i class="el-icon-info"></i> 仓库数据将存储在此目录下，请确保有足够磁盘空间</div>
          </el-form-item>

          <!-- 远程类型：按类型元数据动态渲染 location 字段 -->
          <template v-else>
            <el-row :gutter="14">
              <el-col v-for="f in currentMeta.locationFields" :key="f.key" :span="f.span || 12">
                <el-form-item
                  :label="f.label"
                  :prop="'locationFields.' + f.key"
                  :rules="[{ required: f.required, message: '请输入' + f.label, trigger: 'blur' }]"
                >
                  <el-input v-model="form.locationFields[f.key]" :placeholder="f.placeholder"></el-input>
                </el-form-item>
              </el-col>
            </el-row>
            <el-form-item label="生成的仓库地址 (--repo)" class="mb-0">
              <el-input :value="builtLocation || '（填写完成后自动生成）'" readonly class="text-mono"></el-input>
              <div v-if="typeParseFailed" class="form-tip form-tip--warning">
                <i class="el-icon-warning"></i> 当前仓库地址无法按类型解析，请重新填写后保存
              </div>
            </el-form-item>
          </template>
        </div>

        <!-- 分区 3：后端凭据（仅远程需要凭据的类型显示） -->
        <div class="form-section form-section--cred" v-if="currentMeta.credentialFields.length">
          <div class="form-section__title">
            <i class="el-icon-key"></i> 后端认证凭据
          </div>
          <div class="form-section__desc">
            以下凭据将以环境变量方式传递给 restic，用于连接远程存储后端，与下方仓库加密密码相互独立。
          </div>
          <el-row :gutter="14">
            <el-col v-for="f in currentMeta.credentialFields" :key="f.key" :span="12">
              <el-form-item
                :label="f.label"
                :prop="'credentials.' + f.key"
                :rules="[{ required: f.required, message: '请输入' + f.label, trigger: 'blur' }]"
              >
                <el-input
                  v-model="form.credentials[f.key]"
                  :type="f.type === 'password' ? 'password' : 'text'"
                  :show-password="f.type === 'password'"
                  :placeholder="f.placeholder"
                  autocomplete="new-password"
                ></el-input>
              </el-form-item>
            </el-col>
          </el-row>
        </div>

        <!-- 分区 4：加密安全 -->
        <div class="form-section form-section--last">
          <div class="form-section__title">
            <i class="el-icon-lock"></i> 仓库加密密码
          </div>
          <el-form-item label="加密密码" prop="password" :required="!editingRepository" class="mb-0">
            <el-input
              v-model="form.password"
              type="password"
              show-password
              :placeholder="editingRepository ? '留空则不修改当前密码' : '设置仓库加密密码'"
              autocomplete="new-password"
            ></el-input>
            <div class="form-tip form-tip--warning">
              <i class="el-icon-warning"></i> 密码用于加密所有备份数据，<b>请务必牢记</b>，丢失后将无法恢复数据；与 REST Server 等后端认证密码相互独立。
            </div>
          </el-form-item>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveRepository" :loading="saving">
          {{ editingRepository ? '保存修改' : '创建仓库' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui';
import { repositoryService } from '@/services/repository.service';
import { TASK_TYPES } from '@/services/task.service';
import { pollTask } from '@/utils/task-poller';
import FolderSelector from '@/components/FolderSelector.vue';
import { REPOSITORY_TYPES, REPOSITORY_TYPE_META, buildLocation, parseLocation, typeIcon } from '@/utils/repository-types';

// 后端持久化的统计结果是 JSON 字符串，解析失败时按未统计处理
function parseStats(json) {
  if (!json) return null;
  try {
    return JSON.parse(json);
  } catch (e) {
    return null;
  }
}

export default {
  name: 'Repositories',
  components: { FolderSelector },
  data() {
    const validatePassword = (rule, value, callback) => {
      if (!this.editingRepository && (!value || !String(value).trim())) {
        callback(new Error('请输入仓库密码'));
      } else {
        callback();
      }
    };
    return {
      loading: false,
      saving: false,
      repositories: [],
      searchKeyword: '',
      filterType: '',
      dialogVisible: false,
      editingRepository: null,
      repositoryTypes: REPOSITORY_TYPES,
      typeMeta: REPOSITORY_TYPE_META,
      // locationFields/credentials 按类型动态初始化（整体替换保持响应式），提交前拼装为 path/credentialsJson
      form: { name: '', type: 'local', password: '', locationFields: {}, credentials: {} },
      // 编辑回显时已有 path 无法按类型反解析的兜底提示
      typeParseFailed: false,
      rules: {
        name: [{ required: true, message: '请输入仓库名称', trigger: 'blur' }],
        password: [{ validator: validatePassword, trigger: 'blur' }]
      }
    };
  },
  computed: {
    currentMeta() {
      return this.typeMeta[this.form.type] || this.typeMeta.local;
    },
    // 按当前结构化字段实时拼出的完整 restic location（--repo 值），不完整时为空
    builtLocation() {
      return buildLocation(this.form.type, this.form.locationFields);
    },
    initializedCount() {
      return this.repositories.filter(r => r.isInitialized).length;
    },
    uninitializedCount() {
      return this.repositories.filter(r => !r.isInitialized).length;
    },
    distinctTypeCount() {
      const set = new Set(this.repositories.map(r => r.type || 'local'));
      return set.size;
    },
    hasActiveFilter() {
      return !!(this.searchKeyword && this.searchKeyword.trim()) || !!this.filterType;
    },
    filteredRepositories() {
      let list = this.repositories;
      const kw = (this.searchKeyword || '').trim().toLowerCase();
      if (kw) {
        list = list.filter(r => {
          const name = (r.name || '').toLowerCase();
          const path = (r.path || '').toLowerCase();
          return name.includes(kw) || path.includes(kw);
        });
      }
      if (this.filterType) {
        list = list.filter(r => (r.type || 'local') === this.filterType);
      }
      return list;
    }
  },
  async mounted() {
    await this.fetchRepositories();
    this.resumeActiveTasks();
    if (this.$route.query.action === 'new') {
      this.$nextTick(() => this.openCreateDialog());
    }
  },
  beforeDestroy() {
    // 停止本页所有在途任务的轮询
    this._destroyed = true;
  },
  methods: {
    resetFilters() {
      this.searchKeyword = '';
      this.filterType = '';
    },
    async copyPath(text) {
      if (!text) return;
      try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          await navigator.clipboard.writeText(text);
          Message.success('仓库路径已复制到剪贴板');
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
        if (ok) {
          Message.success('仓库路径已复制到剪贴板');
        } else {
          Message.warning('复制失败，请手动复制');
        }
      } catch (err) {
        Message.warning('复制失败，请手动复制');
      }
    },
    isRowBusy(repo) {
      return !!(repo.initializing || repo.validating || repo.loadingStats || repo.maintenanceLoading);
    },
    getRowBusyText(repo) {
      if (repo.initializing) return '初始化中';
      if (repo.validating) return '校验中';
      if (repo.loadingStats) return '统计中';
      const cmdLabels = {
        'unlock': '解锁中',
        'force-unlock': '强制解锁中',
        'prune': '清理中',
        'deep-check': '深度校验中',
        'repair-index': '修复索引中',
        'repair-snapshots': '修复快照中'
      };
      return cmdLabels[repo.maintenanceCmd] || '运维中';
    },
    getHealthPillClass(status) {
      if (status === 'Passed' || status === 'Healthy') return 'rm-status-pill--success';
      if (status === 'Failed' || status === 'Unhealthy') return 'rm-status-pill--danger';
      if (status === 'Warning') return 'rm-status-pill--warning';
      return 'rm-status-pill--neutral';
    },
    getHealthText(status) {
      const map = {
        Passed: '健康',
        Healthy: '健康',
        Failed: '校验异常',
        Unhealthy: '校验异常',
        Warning: '需关注',
        NeverChecked: '未校验'
      };
      return map[status] || status;
    },
    typeLabel(type) {
      const meta = this.typeMeta[type || 'local'] || this.typeMeta.local;
      return meta.label;
    },
    typeBadgeClass(type) {
      const t = type || 'local';
      const allowed = ['local', 'sftp', 'rest', 's3', 'b2', 'azure', 'gs', 'rclone'];
      return 'repo-type--' + (allowed.includes(t) ? t : 'local');
    },
    handleRowAction(command, repo) {
      if (command === 'validate') return this.validateRepository(repo);
      if (command === 'stats') return this.loadStats(repo);
      if (command === 'edit') return this.handleEditRepository(repo);
      if (command === 'delete') return this.deleteRepository(repo);
      return this.handleMaintenance(command, repo);
    },
    async fetchRepositories() {
      this.loading = true;
      try {
        const list = await repositoryService.getRepositories();
        if (this._destroyed) return;
        // 统计结果已持久化到仓库行，刷新/重进页面后直接恢复展示
        this.repositories = (list || []).map(repo => ({
          ...repo,
          stats: parseStats(repo.statsJson)
        }));
      } catch (e) {
        if (this._destroyed) return;
        Message.error('获取仓库列表失败: ' + (e.message || ''));
      } finally {
        if (!this._destroyed) this.loading = false;
      }
    },
    // 页面重新进入时，恢复本页仓库上仍在执行的后台任务的轮询
    resumeActiveTasks() {
      const active = this.$store.getters['tasks/activeTasks'];
      if (!active || !active.length) return;
      const maintenanceMap = {
        [TASK_TYPES.RepositoryUnlock]: { cmd: 'unlock', title: '解锁仓库' },
        [TASK_TYPES.RepositoryPrune]: { cmd: 'prune', title: '清理未引用数据' },
        [TASK_TYPES.RepositoryDeepCheck]: { cmd: 'deep-check', title: '深度校验' },
        [TASK_TYPES.RepositoryRepairIndex]: { cmd: 'repair-index', title: '修复索引' },
        [TASK_TYPES.RepositoryRepairSnapshots]: { cmd: 'repair-snapshots', title: '修复损坏快照' }
      };
      this.repositories.forEach(repo => {
        active
          .filter(t => t.repositoryId === repo.id)
          .forEach(t => {
            if (t.type === TASK_TYPES.RepositoryInitialize) this.trackTask(t.id, repo, 'initializing', 'init');
            else if (t.type === TASK_TYPES.RepositoryCheck) this.trackTask(t.id, repo, 'validating', 'check');
            else if (t.type === TASK_TYPES.RepositoryStats) this.trackTask(t.id, repo, 'loadingStats', 'stats');
            else if (maintenanceMap[t.type]) {
              const m = maintenanceMap[t.type];
              this.$set(repo, 'maintenanceCmd', m.cmd);
              this.trackTask(t.id, repo, 'maintenanceLoading', 'maintenance', m.title).finally(() => {
                const currentRepo = this.repositories.find(r => r.id === repo.id);
                if (currentRepo) this.$set(currentRepo, 'maintenanceCmd', null);
                if (repo && repo !== currentRepo) this.$set(repo, 'maintenanceCmd', null);
              });
            }
          });
      });
    },
    // 提交仓库类后台任务并跟踪到完成（按钮在任务期间保持 loading）
    async submitAndTrack(repo, flag, kind, submitFn, title = null) {
      let res;
      try {
        res = await submitFn();
      } catch (e) {
        Message.error(e.response?.data?.message || e.message);
        return;
      }
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      return this.trackTask(res.taskId, repo, flag, kind, title);
    },
    // 轮询单个后台任务直到终态并提示结果（title 用于运维类任务，成功/失败提示都以它命名）
    async trackTask(taskId, repo, flag, kind, title = null) {
      const repoId = repo.id;
      this.$set(repo, flag, true);
      try {
        const task = await pollTask(taskId, { shouldStop: () => this._destroyed });
        if (this._destroyed) return;
        let result = null;
        if (task.resultJson) {
          try {
            result = JSON.parse(task.resultJson);
          } catch (e) {
            result = null;
          }
        }
        const targetRepo = this.repositories.find(r => r.id === repoId) || repo;
        if (task.status === 'Succeeded') {
          if (title) Message.success(title + '完成');
          else if (kind === 'init') {
            this.$set(targetRepo, 'isInitialized', true);
            Message.success('仓库初始化成功');
          } else if (kind === 'check') {
            if (result && result.status === 'Passed') {
              this.$set(targetRepo, 'isInitialized', true);
              Message.success('仓库结构有效');
            } else {
              Message.error('仓库结构验证失败');
            }
          } else if (kind === 'stats' && result) {
            this.$set(targetRepo, 'stats', result);
            this.$set(targetRepo, 'statsUpdatedAt', task.endTime);
          }
        } else {
          const label = title ? title + '失败' : kind === 'init' ? '初始化失败' : kind === 'check' ? '校验失败' : '统计失败';
          Message.error(label + ': ' + (task.error || ''));
        }
      } catch (e) {
        if (e.message !== 'cancelled') Message.error(e.response?.data?.message || e.message);
      } finally {
        const currentRepo = this.repositories.find(r => r.id === repoId);
        if (currentRepo) this.$set(currentRepo, flag, false);
        if (repo && repo !== currentRepo) this.$set(repo, flag, false);
      }
    },
    // 切换类型时按新类型元数据重置动态字段（整体替换保持 Vue 2 响应式）
    handleTypeChange() {
      const locationFields = {};
      this.currentMeta.locationFields.forEach(f => { locationFields[f.key] = ''; });
      const credentials = {};
      this.currentMeta.credentialFields.forEach(f => { credentials[f.key] = ''; });
      this.form.locationFields = locationFields;
      this.form.credentials = credentials;
      this.typeParseFailed = false;
      this.$nextTick(() => { this.$refs.formRef && this.$refs.formRef.clearValidate(); });
    },
    openCreateDialog() {
      this.editingRepository = null;
      this.form = { name: '', type: 'local', password: '', locationFields: {}, credentials: {} };
      this.typeParseFailed = false;
      this.dialogVisible = true;
      this.$nextTick(() => this.handleTypeChange());
    },
    async saveRepository() {
      try {
        const valid = await this.$refs.formRef.validate();
        if (!valid) return;
      } catch (e) {
        return;
      }

      try {
        // 拼装完整 location；local 直接取路径，其余类型按结构化字段生成
        const path = this.form.type === 'local'
          ? (this.form.locationFields.path || '').trim()
          : this.builtLocation;
        if (!path) {
          Message.error('请完整填写仓库地址字段');
          return;
        }

        // 收集非空凭据为 credentialsJson（键与后端环境变量映射一致）
        const cred = {};
        this.currentMeta.credentialFields.forEach(f => {
          const val = (this.form.credentials[f.key] || '').trim();
          if (val) cred[f.key] = val;
        });
        const payload = {
          name: this.form.name,
          type: this.form.type,
          path,
          password: this.form.password,
          credentialsJson: Object.keys(cred).length ? JSON.stringify(cred) : null
        };
        if (this.editingRepository) {
          payload.id = this.editingRepository.id;
          payload.isInitialized = this.editingRepository.isInitialized;
        }

        this.saving = true;
        if (this.editingRepository) {
          await repositoryService.updateRepository(this.editingRepository.id, payload);
          Message.success('仓库已更新');
        } else {
          await repositoryService.createRepository(payload);
          Message.success('仓库创建成功');
        }
        this.dialogVisible = false;
        await this.fetchRepositories();
      } catch (error) {
        if (error === false) return;
        Message.error(error.response?.data?.message || error.message);
      } finally {
        this.saving = false;
      }
    },
    async deleteRepository(repo) {
      try {
        await MessageBox.confirm(`确定要删除仓库 "${repo.name}" 吗？仓库数据仍保留在磁盘上。`, '删除确认', {
          confirmButtonText: '确定删除',
          cancelButtonText: '取消',
          type: 'warning'
        });
      } catch {
        return;
      }
      try {
        await repositoryService.deleteRepository(repo.id);
        Message.success('仓库已删除');
        await this.fetchRepositories();
      } catch (e) {
        Message.error(e.response?.data?.message || e.message);
      }
    },
    async initializeRepository(repo) {
      await this.submitAndTrack(repo, 'initializing', 'init', () => repositoryService.initializeRepository(repo.id));
    },
    async validateRepository(repo) {
      await this.submitAndTrack(repo, 'validating', 'check', () => repositoryService.validateRepository(repo.id));
    },
    async loadStats(repo) {
      await this.submitAndTrack(repo, 'loadingStats', 'stats', () => repositoryService.getRepositoryStats(repo.id));
    },
    // 运维操作（与快照页「运维」菜单一致，同样走后台任务）
    async handleMaintenance(command, repo) {
      const meta = {
        'unlock': { title: '解锁仓库', confirm: '将清理该仓库中陈旧的锁，确定继续吗？', fn: () => repositoryService.unlockRepository(repo.id) },
        'force-unlock': { title: '强制解锁', confirm: '强制解锁会移除仓库中的全部锁，包括其他机器或进程正在持有的锁，可能导致正在进行的备份/删除出错。仅在确认没有其他程序在用该仓库时执行，确定继续吗？', fn: () => repositoryService.unlockRepository(repo.id, true) },
        'prune': { title: '清理未引用数据', confirm: 'prune 会永久删除未被任何快照引用的数据，可能耗时较长，确定继续吗？', fn: () => repositoryService.pruneRepository(repo.id) },
        'deep-check': { title: '深度校验', confirm: '将读取约 5% 的实际数据包进行校验，耗时较长，确定继续吗？', fn: () => repositoryService.deepCheckRepository(repo.id, '5%') },
        'repair-index': { title: '修复索引', confirm: '将重建仓库索引，确定继续吗？', fn: () => repositoryService.repairIndex(repo.id) },
        'repair-snapshots': { title: '修复损坏快照', confirm: '将移除快照中无法读取的数据并生成新快照，确定继续吗？', fn: () => repositoryService.repairSnapshots(repo.id) }
      }[command];
      if (!meta) return;

      try {
        await MessageBox.confirm(meta.confirm, meta.title, { type: 'warning', confirmButtonText: '继续', cancelButtonText: '取消' });
      } catch {
        return;
      }

      // maintenanceCmd 标记当前跑的是哪一项
      this.$set(repo, 'maintenanceCmd', command);
      try {
        await this.submitAndTrack(repo, 'maintenanceLoading', 'maintenance', meta.fn, meta.title);
      } finally {
        const currentRepo = this.repositories.find(r => r.id === repo.id);
        if (currentRepo) this.$set(currentRepo, 'maintenanceCmd', null);
        if (repo && repo !== currentRepo) this.$set(repo, 'maintenanceCmd', null);
      }
    },
    viewSnapshots(repo) {
      this.$router.push(`/repositories/${repo.id}/snapshots`);
    },
    handleEditRepository(repo) {
      this.editingRepository = repo;
      const type = repo.type || 'local';
      // 尽力把已有 location 反解析回结构化字段；失败时置空并提示重新填写
      const fields = parseLocation(type, repo.path);
      this.typeParseFailed = !fields;
      let credentials = {};
      if (repo.credentialsJson) {
        try {
          credentials = JSON.parse(repo.credentialsJson) || {};
        } catch (e) {
          credentials = {};
        }
      }
      this.form = { name: repo.name, type, password: '', locationFields: fields || {}, credentials };
      // 反解析结果只含已填写的键，补齐元数据里的全部字段键（保证 v-model 键存在）；
      // 不走 handleTypeChange——那会把回显值清空
      this.$nextTick(() => this.mergeFormFields());
      this.dialogVisible = true;
    },
    // 把缺失的动态字段键补为空串（对象整体替换，保持响应式；不清空已回显的值）
    mergeFormFields() {
      const locationFields = {};
      this.currentMeta.locationFields.forEach(f => { locationFields[f.key] = this.form.locationFields[f.key] || ''; });
      const credentials = {};
      this.currentMeta.credentialFields.forEach(f => { credentials[f.key] = this.form.credentials[f.key] || ''; });
      this.form.locationFields = locationFields;
      this.form.credentials = credentials;
      this.$nextTick(() => { this.$refs.formRef && this.$refs.formRef.clearValidate(); });
    },
    typeIcon(type) {
      return typeIcon(type);
    },
    formatFileSize(b) {
      if (!b) return '0 B';
      const k = 1024;
      const units = ['B', 'KB', 'MB', 'GB', 'TB'];
      const i = Math.floor(Math.log(b) / Math.log(k));
      return parseFloat((b / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
    },
    formatDate(d) {
      if (!d) return '-';
      try {
        return new Date(d).toLocaleString('zh-CN', { hour12: false });
      } catch {
        return '-';
      }
    }
  }
};
</script>

<style scoped>
.repositories-page {
  padding-bottom: 24px;
}

.repo-kpi-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.repo-kpi-icon {
  font-size: 16px;
}
.repo-kpi-icon--brand { color: var(--brand-400); }
.repo-kpi-icon--success { color: var(--success-500); }
.repo-kpi-icon--warning { color: var(--warning-500); }

.toolbar-search {
  width: 260px;
  max-width: 100%;
}

.toolbar-select {
  width: 168px;
}

.toolbar-summary {
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* 仓库单元格 */
.repo-cell {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.repo-cell__icon {
  width: 38px;
  height: 38px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 17px;
  border: 1px solid transparent;
}

.repo-cell__body {
  min-width: 0;
  flex: 1;
}

.repo-cell__top {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.repo-cell__name {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 14px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.repo-type-badge {
  display: inline-flex;
  align-items: center;
  padding: 1px 8px;
  border-radius: var(--radius-full);
  font-size: 11px;
  font-weight: 600;
  line-height: 1.5;
  flex-shrink: 0;
  border: 1px solid transparent;
}

.repo-type--local {
  background: var(--brand-50);
  color: var(--brand-400);
  border-color: var(--brand-200);
}
.repo-type--sftp {
  background: var(--info-50);
  color: var(--info-600);
  border-color: var(--color-border);
}
.repo-type--rest {
  background: var(--success-50);
  color: var(--success-600);
  border-color: var(--color-border);
}
.repo-type--s3 {
  background: var(--warning-50);
  color: var(--warning-600);
  border-color: var(--color-border);
}
.repo-type--b2 {
  background: var(--danger-50);
  color: var(--danger-600);
  border-color: var(--color-border);
}
.repo-type--azure {
  background: var(--info-50);
  color: var(--info-500);
  border-color: var(--color-border);
}
.repo-type--gs {
  background: var(--success-50);
  color: var(--success-500);
  border-color: var(--color-border);
}
.repo-type--rclone {
  background: var(--brand-100);
  color: var(--brand-300);
  border-color: var(--brand-200);
}

.repo-cell__path-wrap {
  margin-top: 5px;
}

.repo-path-chip {
  max-width: 100%;
}

.status-stack {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
}

.num-cell {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 13px;
}

.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  white-space: nowrap;
}

.dropdown-item-danger {
  color: var(--danger-600) !important;
}

/* 空状态 */
.empty-state {
  text-align: center;
  padding: 64px 20px;
}

.empty-state__icon {
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

.empty-state__title {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.empty-state__desc {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin-top: 6px;
}

.empty-state__actions {
  margin-top: 16px;
  display: inline-flex;
  gap: 10px;
}

/* 弹窗分组卡片 */
.form-section {
  padding: 14px 16px;
  margin-bottom: 14px;
  border-radius: var(--radius-md);
  background: var(--color-bg-soft);
  border: 1px solid var(--color-border);
}

.form-section--cred {
  border-style: dashed;
  border-color: var(--brand-200);
  background: var(--brand-50);
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

.form-section__desc {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin: -4px 0 12px;
  line-height: 1.5;
}

.form-tip {
  margin-top: 6px;
  font-size: 12px;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.form-tip--inline {
  margin-top: 0;
}

.form-tip--warning {
  color: var(--warning-600);
}

.form-tip i {
  margin-right: 4px;
}

.mb-0 {
  margin-bottom: 0 !important;
}

@media (max-width: 768px) {
  .repo-kpi-grid {
    grid-template-columns: 1fr;
  }
  .toolbar-search,
  .toolbar-select {
    width: 100%;
  }
}
</style>