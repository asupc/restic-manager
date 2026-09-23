<template>
  <div class="backup-mgmt-page">
    <div class="page-header">
      <div>
        <h2 class="page-header__title">快照浏览</h2>
        <div class="page-header__subtitle">跨仓库浏览所有历史快照,支持比较与恢复</div>
      </div>
      <div class="page-header__actions">
        <el-select v-model="selectedRepository" placeholder="选择仓库" @change="loadSnapshots" size="small" class="repo-select">
          <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id"></el-option>
        </el-select>
        <el-button round icon="el-icon-download" size="small" :loading="syncing" @click="syncSnapshotsFromRemote" :disabled="!selectedRepository">同步远端</el-button>
        <el-button round icon="el-icon-refresh" size="small" :loading="loading" @click="loadSnapshots">刷新</el-button>
      </div>
    </div>

    <snapshot-list
      v-if="selectedRepository"
      :snapshots="snapshots"
      :repository-id="selectedRepository"
      :show-details="true"
      :loading="loading"
      @restore-snapshot="handleRestoreSnapshot"
      @delete-snapshot="handleDeleteSnapshot"
      @delete-snapshots="handleBatchDeleteSnapshots"
    ></snapshot-list>

    <div v-else class="empty">
      <div class="empty__title">选择一个仓库查看快照</div>
      <div class="empty__desc">从右上角下拉框选择已配置的仓库</div>
    </div>
  </div>
</template>

<script>
import { Message } from 'element-ui';
import { repositoryService } from '@/services/repository.service';
import { TASK_TYPES } from '@/services/task.service';
import { pollTask } from '@/utils/task-poller';
import SnapshotList from '@/components/SnapshotList.vue';

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
  methods: {
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
      if (!this.selectedRepository) return;
      this.loading = true;
      try {
        this.snapshots = await repositoryService.getSnapshots(this.selectedRepository);
      } catch (e) {
        Message.error('获取快照失败: ' + e.message);
      } finally {
        this.loading = false;
      }
    },
    // 提交后台任务后轮询到终态并提示；返回任务是否成功
    async awaitTask(taskId, { loadingFlag = null, label = '操作', successMessage = null } = {}) {
      if (loadingFlag) this[loadingFlag] = true;
      let ok = false;
      try {
        const task = await pollTask(taskId, { shouldStop: () => this._destroyed });
        if (this._destroyed) return false;
        ok = task.status === 'Succeeded';
        if (ok) {
          Message.success(successMessage ? successMessage(task) : label + '完成');
        } else {
          Message.error(label + '失败: ' + (task.error || ''));
        }
      } catch (e) {
        if (e.message !== 'cancelled') Message.error(label + '失败: ' + (e.message || ''));
      } finally {
        if (loadingFlag) this[loadingFlag] = false;
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
          const r = task.resultJson ? JSON.parse(task.resultJson) : {};
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
          const r = task.resultJson ? JSON.parse(task.resultJson) : {};
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
          const r = task.resultJson ? JSON.parse(task.resultJson) : {};
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
      const active = this.$store.getters['tasks/activeByRepository'](this.selectedRepository);
      active.forEach(t => {
        switch (t.type) {
          case TASK_TYPES.SnapshotSync:
            this.awaitTask(t.id, { loadingFlag: 'syncing', label: '同步' })
              .then(ok => { if (ok) this.loadSnapshots(); });
            break;
          case TASK_TYPES.SnapshotRestore:
          case TASK_TYPES.SnapshotDelete:
          case TASK_TYPES.SnapshotTagSet:
            this.awaitTask(t.id, { label: t.title || '操作' })
              .then(() => this.loadSnapshots());
            break;
          default:
            break;
        }
      });
    }
  },
  watch: {
    // 切换仓库时恢复该仓库的在途任务跟踪
    selectedRepository() {
      this.resumeActiveTasks();
    }
  },
  async mounted() {
    await this.fetchRepositories();
    this.resumeActiveTasks();
  },
  beforeDestroy() {
    // 停止本页所有在途任务的轮询
    this._destroyed = true;
  }
};
</script>

<style scoped>
.backup-mgmt-page { padding-bottom: 24px; }
.repo-select { width: 200px; }

.empty { text-align: center; padding: 80px 20px; }
.empty__title { font-size: 18px; font-weight: 600; color: var(--color-text-primary); }
.empty__desc { font-size: 14px; color: var(--color-text-secondary); margin-top: 6px; }
</style>
