<template>
  <div class="backup-mgmt-page">
    <div class="page-header">
      <div>
        <h2 class="page-header__title">快照浏览</h2>
        <div class="page-header__subtitle">跨仓库浏览历史快照，支持文件检视、标签管理、差异对比与数据恢复</div>
      </div>
    </div>

    <!-- 上下文横幅卡片 -->
    <div class="rm-surface repo-context-banner mb-4">
      <div class="repo-context-banner__left">
        <div class="repo-selector-wrap">
          <span class="repo-selector-label">当前仓库</span>
          <el-select
            v-model="selectedRepository"
            placeholder="请选择备份仓库"
            @change="loadSnapshots"
            size="small"
            filterable
            class="repo-select"
          >
            <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id">
              <div class="repo-option">
                <i :class="typeIcon(repo.type)"></i>
                <span class="repo-option__name">{{ repo.name }}</span>
                <span class="repo-option__type">{{ typeLabel(repo.type) }}</span>
              </div>
            </el-option>
          </el-select>
        </div>

        <div v-if="currentRepo" class="repo-context-meta">
          <span class="repo-type-badge" :class="typeBadgeClass(currentRepo.type)">
            <i :class="typeIcon(currentRepo.type)"></i>
            {{ typeLabel(currentRepo.type) }}
          </span>
          <span
            class="rm-status-pill"
            :class="getRepoStatusPillClass(currentRepo)"
          >
            <span class="rm-status-pill__dot"></span>
            {{ getRepoStatusText(currentRepo) }}
          </span>
          <span class="rm-code-chip repo-context-path" :title="currentRepo.path">
            <span class="rm-code-chip__text">{{ currentRepo.path }}</span>
            <button
              type="button"
              class="rm-code-chip__copy"
              title="复制仓库路径"
              @click.stop="copyPath(currentRepo.path)"
            >
              <i class="el-icon-document-copy"></i>
            </button>
          </span>
        </div>
      </div>

      <div class="repo-context-banner__right">
        <el-button
          round
          icon="el-icon-download"
          size="small"
          type="primary"
          plain
          :loading="syncing"
          :disabled="!selectedRepository"
          @click="syncSnapshotsFromRemote"
        >同步快照</el-button>
        <el-button
          round
          icon="el-icon-refresh"
          size="small"
          :loading="loading"
          :disabled="!selectedRepository"
          @click="loadSnapshots"
        >刷新</el-button>
        <el-button
          round
          icon="el-icon-top-right"
          size="small"
          :disabled="!selectedRepository"
          @click="goToRepoDetail"
        >进入仓库详情</el-button>
      </div>
    </div>

    <!-- 快照列表 -->
    <snapshot-list
      v-if="selectedRepository"
      :snapshots="snapshots"
      :repository-id="selectedRepository"
      :show-details="true"
      :loading="loading"
      @restore-snapshot="handleRestoreSnapshot"
      @delete-snapshot="handleDeleteSnapshot"
      @delete-snapshots="handleBatchDeleteSnapshots"
      @tags-updated="loadSnapshots"
    ></snapshot-list>

    <!-- 引导式空状态卡片 -->
    <div v-else class="rm-surface empty-guide-card">
      <div class="empty-guide-card__icon">
        <i :class="repositories.length === 0 ? 'el-icon-folder-add' : 'el-icon-camera'"></i>
      </div>
      <div class="empty-guide-card__title">
        {{ repositories.length === 0 ? '还没有可用的备份仓库' : '请选择一个仓库开始浏览快照' }}
      </div>
      <div class="empty-guide-card__desc">
        {{
          repositories.length === 0
            ? '先创建一个本地或云端备份仓库并完成初始化，之后即可在此浏览与恢复历史快照。'
            : '从上方仓库选择器中切换目标仓库，即可查看该仓库内的全部历史快照与文件变更。'
        }}
      </div>
      <div class="empty-guide-card__actions">
        <el-button
          type="primary"
          round
          icon="el-icon-plus"
          size="small"
          @click="$router.push('/repositories?action=new')"
        >前往创建仓库</el-button>
        <el-button
          v-if="repositories.length > 0"
          round
          size="small"
          @click="selectFirstRepository"
        >选择第一个仓库</el-button>
      </div>
    </div>
  </div>
</template>

<script>
import { Message } from 'element-ui';
import { repositoryService } from '@/services/repository.service';
import { TASK_TYPES } from '@/services/task.service';
import { pollTask } from '@/utils/task-poller';
import { REPOSITORY_TYPE_META, typeIcon } from '@/utils/repository-types';
import SnapshotList from '@/components/SnapshotList.vue';

function safeParseJson(json, fallback = {}) {
  if (!json) return fallback;
  try {
    const parsed = JSON.parse(json);
    return parsed === null || parsed === undefined ? fallback : parsed;
  } catch (e) {
    return fallback;
  }
}

export default {
  name: 'BackupManagement',
  components: { SnapshotList },
  data() {
    return {
      repositories: [],
      selectedRepository: '',
      snapshots: [],
      loading: false,
      syncing: false
    };
  },
  computed: {
    currentRepo() {
      if (!this.selectedRepository) return null;
      return this.repositories.find(r => r.id === this.selectedRepository) || null;
    }
  },
  created() {
    this._repoSwitchToken = 0;
    this._trackedTaskIds = new Set();
    this._snapshotReqId = 0;
  },
  methods: {
    typeIcon(type) {
      return typeIcon(type);
    },
    typeLabel(type) {
      const meta = REPOSITORY_TYPE_META[type || 'local'] || REPOSITORY_TYPE_META.local;
      return meta.label;
    },
    typeBadgeClass(type) {
      const t = type || 'local';
      const allowed = ['local', 'sftp', 'rest', 's3', 'b2', 'azure', 'gs', 'rclone'];
      return 'repo-type--' + (allowed.includes(t) ? t : 'local');
    },
    getRepoStatusPillClass(repo) {
      if (!repo.isInitialized) return 'rm-status-pill--warning';
      if (repo.healthStatus === 'Failed' || repo.healthStatus === 'Unhealthy') return 'rm-status-pill--danger';
      if (repo.healthStatus === 'Warning') return 'rm-status-pill--warning';
      return 'rm-status-pill--success';
    },
    getRepoStatusText(repo) {
      if (!repo.isInitialized) return '未初始化';
      if (repo.healthStatus === 'Failed' || repo.healthStatus === 'Unhealthy') return '健康异常';
      if (repo.healthStatus === 'Warning') return '需关注';
      return '已初始化 · 健康';
    },
    goToRepoDetail() {
      if (!this.selectedRepository) return;
      this.$router.push(`/repositories/${this.selectedRepository}/snapshots`);
    },
    selectFirstRepository() {
      if (!this.repositories.length) return;
      this.selectedRepository = this.repositories[0].id;
      this.loadSnapshots();
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
        // 降级方案
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
        if (ok) Message.success('仓库路径已复制到剪贴板');
        else Message.warning('复制失败，请手动复制');
      } catch (err) {
        Message.warning('复制失败，请手动复制');
      }
    },
    async fetchRepositories() {
      try {
        this.repositories = await repositoryService.getRepositories();
        if (this.repositories.length > 0 && !this.selectedRepository) {
          this.selectedRepository = this.repositories[0].id;
          await this.loadSnapshots();
        }
      } catch (e) {
        Message.error('获取仓库列表失败: ' + e.message);
      }
    },
    async loadSnapshots() {
      if (!this.selectedRepository) {
        this.snapshots = [];
        this.loading = false;
        return;
      }
      const reqId = ++this._snapshotReqId;
      this.loading = true;
      try {
        const list = await repositoryService.getSnapshots(this.selectedRepository);
        if (this._destroyed || reqId !== this._snapshotReqId) return;
        this.snapshots = list;
      } catch (e) {
        if (this._destroyed || reqId !== this._snapshotReqId) return;
        Message.error('获取快照失败: ' + e.message);
      } finally {
        if (!this._destroyed && reqId === this._snapshotReqId) {
          this.loading = false;
        }
      }
    },
    // 提交后台任务后轮询到终态并提示；返回任务是否成功
    async awaitTask(taskId, { loadingFlag = null, label = '操作', successMessage = null, token = this._repoSwitchToken } = {}) {
      if (this._trackedTaskIds.has(taskId)) return false;
      this._trackedTaskIds.add(taskId);
      const myToken = token;
      if (loadingFlag) this[loadingFlag] = true;
      let ok = false;
      try {
        const task = await pollTask(taskId, { shouldStop: () => this._destroyed || myToken !== this._repoSwitchToken });
        if (this._destroyed || myToken !== this._repoSwitchToken) return false;
        ok = task.status === 'Succeeded';
        if (ok) {
          Message.success(successMessage ? successMessage(task) : label + '完成');
        } else {
          Message.error(label + '失败: ' + (task.error || ''));
        }
      } catch (e) {
        if (this._destroyed || myToken !== this._repoSwitchToken) return false;
        if (e.message !== 'cancelled') Message.error(label + '失败: ' + (e.message || ''));
      } finally {
        if (myToken === this._repoSwitchToken) {
          this._trackedTaskIds.delete(taskId);
          if (loadingFlag) this[loadingFlag] = false;
        }
      }
      return ok;
    },
    // 从远端仓库全量同步快照元数据到本地缓存（后台任务）
    async syncSnapshotsFromRemote() {
      if (!this.selectedRepository) return;
      let res;
      try {
        res = await repositoryService.syncSnapshots(this.selectedRepository);
      } catch (e) {
        Message.error(e.response?.data?.message || e.message || '');
        return;
      }
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      const ok = await this.awaitTask(res.taskId, {
        loadingFlag: 'syncing',
        label: '同步',
        successMessage: (task) => {
          const r = safeParseJson(task.resultJson, {});
          const n = v => (v === null || v === undefined ? 0 : v);
          return `同步完成: 新增 ${n(r.added)}、更新 ${n(r.updated)}、标记已删除 ${n(r.markedDeleted)}（远端共 ${n(r.remoteTotal)} 条）`;
        }
      });
      if (ok) await this.loadSnapshots();
    },
    async handleRestoreSnapshot(data) {
      const { repositoryId, snapshot, targetPath, verify } = data;
      if (!repositoryId || !snapshot) return;
      let res;
      try {
        res = await repositoryService.restoreSnapshot(repositoryId, snapshot.id, targetPath, verify);
      } catch (e) {
        Message.error(e.response?.data?.message || e.message || '');
        return;
      }
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      await this.awaitTask(res.taskId, { label: '恢复', successMessage: () => '恢复成功' });
    },
    async handleDeleteSnapshot(data) {
      const { repositoryId, snapshot } = data;
      if (!repositoryId || !snapshot) return;
      let res;
      try {
        res = await repositoryService.deleteSnapshot(repositoryId, snapshot.id);
      } catch (e) {
        Message.error(e.response?.data?.message || e.message || '');
        return;
      }
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      const ok = await this.awaitTask(res.taskId, {
        label: '删除',
        // resultJson 带 localCacheWarning 时说明远端已删但本地缓存更新失败
        successMessage: (task) => {
          const r = safeParseJson(task.resultJson, {});
          return r.localCacheWarning ? '快照已删除；' + r.localCacheWarning : '快照已删除';
        }
      });
      if (ok) await this.loadSnapshots();
    },
    async handleBatchDeleteSnapshots(data) {
      const { repositoryId, snapshots } = data;
      if (!repositoryId || !snapshots || !snapshots.length) return;
      let res;
      try {
        res = await repositoryService.batchDeleteSnapshots(repositoryId, snapshots.map(s => s.id));
      } catch (e) {
        Message.error(e.response?.data?.message || e.message || '');
        return;
      }
      this.$store.dispatch('tasks/noteSubmitted', res.taskId);
      const ok = await this.awaitTask(res.taskId, {
        label: '批量删除',
        successMessage: (task) => {
          const r = safeParseJson(task.resultJson, {});
          const n = v => (v === null || v === undefined ? 0 : v);
          let msg = `批量删除完成: 远端删除 ${n(r.deleted)} 个，本地标记 ${n(r.markedDeleted)} 个`;
          if (r.localCacheWarning) msg += '；' + r.localCacheWarning;
          return msg;
        }
      });
      if (ok) await this.loadSnapshots();
    },
    // 切换仓库 / 页面重新进入时，恢复该仓库仍在执行的后台任务的轮询
    resumeActiveTasks() {
      if (!this.selectedRepository) return;
      const token = this._repoSwitchToken;
      const active = this.$store.getters['tasks/activeByRepository'](this.selectedRepository);
      active.forEach(t => {
        if (this._trackedTaskIds.has(t.id)) return;
        switch (t.type) {
          case TASK_TYPES.SnapshotSync:
            this.awaitTask(t.id, { loadingFlag: 'syncing', label: '同步', token })
              .then(ok => {
                if (ok && !this._destroyed && token === this._repoSwitchToken) {
                  this.loadSnapshots();
                }
              });
            break;
          case TASK_TYPES.SnapshotRestore:
          case TASK_TYPES.FileRestore:
          case TASK_TYPES.SnapshotDelete:
          case TASK_TYPES.SnapshotBatchDelete:
          case TASK_TYPES.SnapshotTagSet:
            this.awaitTask(t.id, { label: t.title || '操作', token })
              .then(() => {
                if (!this._destroyed && token === this._repoSwitchToken) {
                  this.loadSnapshots();
                }
              });
            break;
          default:
            break;
        }
      });
    }
  },
  watch: {
    // 切换仓库时取消旧仓库轮询、清空旧快照并恢复新仓库的在途任务跟踪
    selectedRepository() {
      this._repoSwitchToken += 1;
      this._trackedTaskIds = new Set();
      this.syncing = false;
      this.snapshots = [];
      this.resumeActiveTasks();
    }
  },
  async mounted() {
    await this.fetchRepositories();
  },
  beforeDestroy() {
    // 停止本页所有在途任务的轮询
    this._destroyed = true;
    this._repoSwitchToken += 1;
  }
};
</script>

<style scoped>
.backup-mgmt-page {
  padding-bottom: 24px;
}

/* 顶部上下文横幅卡片 */
.repo-context-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
  padding: 16px 20px;
}

.repo-context-banner__left {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 14px;
  min-width: 0;
  flex: 1;
}

.repo-selector-wrap {
  display: flex;
  align-items: center;
  gap: 10px;
}

.repo-selector-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.repo-select {
  width: 220px;
}

.repo-option {
  display: flex;
  align-items: center;
  gap: 8px;
}

.repo-option__name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
}

.repo-option__type {
  font-size: 11px;
  color: var(--color-text-secondary);
}

.repo-context-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  min-width: 0;
}

.repo-type-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 3px 10px;
  border-radius: var(--radius-full);
  font-size: 12px;
  font-weight: 600;
  border: 1px solid transparent;
  white-space: nowrap;
}

.repo-type--local { background: var(--brand-50); color: var(--brand-400); border-color: var(--brand-200); }
.repo-type--sftp { background: var(--info-50); color: var(--info-600); border-color: var(--color-border); }
.repo-type--rest { background: var(--success-50); color: var(--success-600); border-color: var(--color-border); }
.repo-type--s3 { background: var(--warning-50); color: var(--warning-600); border-color: var(--color-border); }
.repo-type--b2 { background: var(--danger-50); color: var(--danger-600); border-color: var(--color-border); }
.repo-type--azure { background: var(--info-50); color: var(--info-500); border-color: var(--color-border); }
.repo-type--gs { background: var(--success-50); color: var(--success-500); border-color: var(--color-border); }
.repo-type--rclone { background: var(--brand-100); color: var(--brand-300); border-color: var(--brand-200); }

.repo-context-path {
  max-width: 360px;
}

.repo-context-banner__right {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

/* 引导式空状态卡片 */
.empty-guide-card {
  text-align: center;
  padding: 72px 24px;
}

.empty-guide-card__icon {
  width: 64px;
  height: 64px;
  border-radius: var(--radius-xl);
  background: var(--brand-50);
  border: 1px solid var(--brand-200);
  color: var(--brand-400);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  margin-bottom: 16px;
}

.empty-guide-card__title {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.empty-guide-card__desc {
  max-width: 460px;
  margin: 8px auto 0;
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.65;
}

.empty-guide-card__actions {
  margin-top: 20px;
  display: inline-flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  justify-content: center;
}

@media (max-width: 768px) {
  .repo-select {
    width: 100%;
  }
  .repo-selector-wrap {
    width: 100%;
  }
  .repo-context-path {
    max-width: 100%;
  }
}
</style>
