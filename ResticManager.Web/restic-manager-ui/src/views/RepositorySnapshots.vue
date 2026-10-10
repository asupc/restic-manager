<template>
    <div class="snapshots-page">
        <div class="page-header">
            <div>
                <el-page-header @back="goBack" :content="`仓库 ${repository?.name || ''} · 快照`">
                </el-page-header>
                <div v-if="repository" class="repo-info mt-2">
                    <span class="text-mono text-secondary" style="font-size:12px;">
                        <i :class="repoTypeIcon(repository.type)"></i> {{ repository.path }}
                    </span>
                    <span class="rm-badge rm-badge--success" v-if="repository.isInitialized">已初始化</span>
                    <span class="rm-badge rm-badge--warning" v-else>未初始化</span>
                </div>
            </div>
            <div class="page-header__actions">
                <el-button round icon="el-icon-search" size="small" @click="openSearch" :disabled="!repository || !repository.isInitialized">搜索文件</el-button>
                <el-dropdown trigger="click" @command="handleMaintenance" :disabled="maintenanceLoading">
                    <el-button round icon="el-icon-setting" size="small" :loading="maintenanceLoading">
                        运维<i class="el-icon-arrow-down el-icon--right"></i>
                    </el-button>
                    <el-dropdown-menu slot="dropdown">
                        <el-dropdown-item command="unlock" icon="el-icon-unlock">解锁(清理陈旧锁)</el-dropdown-item>
                        <el-dropdown-item command="force-unlock" icon="el-icon-warning-outline">强制解锁(移除所有锁)</el-dropdown-item>
                        <el-dropdown-item command="prune" icon="el-icon-delete">清理未引用数据(prune)</el-dropdown-item>
                        <el-dropdown-item command="deep-check" icon="el-icon-cpu">深度校验(读 5% 数据)</el-dropdown-item>
                        <el-dropdown-item command="repair-index" icon="el-icon-refresh" divided>修复索引</el-dropdown-item>
                        <el-dropdown-item command="repair-snapshots" icon="el-icon-first-aid-kit">修复损坏快照</el-dropdown-item>
                    </el-dropdown-menu>
                </el-dropdown>
                <el-button round icon="el-icon-download" size="small" :loading="syncing" @click="syncSnapshotsFromRemote" :disabled="!repository || !repository.isInitialized">同步远端</el-button>
                <el-button round icon="el-icon-refresh" size="small" :loading="loading" @click="fetchSnapshots">刷新</el-button>
            </div>
        </div>

        <snapshot-list
            :snapshots="snapshots"
            :repository-id="$route.params.id"
            :repository="repository"
            :loading="loading"
            @restore-snapshot="handleRestoreSnapshot"
            @delete-snapshot="handleDeleteSnapshot"
            @delete-snapshots="handleBatchDeleteSnapshots"
            @tags-updated="fetchSnapshots"
        ></snapshot-list>

        <!-- 跨快照文件搜索 -->
        <el-dialog :visible.sync="searchDialogVisible" title="跨快照搜索文件" width="640px" :close-on-click-modal="false" append-to-body>
            <div class="search-bar">
                <el-input
                    v-model="searchPattern"
                    placeholder="输入文件名或通配符,如 *.docx"
                    prefix-icon="el-icon-search"
                    clearable
                    @keyup.enter.native="performSearch"
                ></el-input>
                <el-button type="primary" icon="el-icon-search" :loading="searching" @click="performSearch">搜索</el-button>
            </div>
            <div v-if="searchError" class="search-error">{{ searchError }}</div>
            <div v-else-if="searched && searchResults.length === 0 && !searching" class="search-empty">未找到匹配的文件</div>
            <div v-else class="search-results">
                <div v-for="r in searchResults" :key="r.snapshot" class="search-result">
                    <div class="search-result__head">
                        <i class="el-icon-time"></i>
                        <span class="text-mono">{{ (r.snapshot || '').substring(0, 8) }}</span>
                        <span class="text-secondary">{{ r.hits }} 处命中</span>
                    </div>
                    <div v-for="(m, i) in r.matches" :key="i" class="search-result__file">
                        <span class="text-mono">{{ m.path }}</span>
                        <span class="text-secondary">{{ formatSize(m.size) }}</span>
                    </div>
                </div>
            </div>
        </el-dialog>
    </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui';
import { repositoryService } from '@/services/repository.service';
import { TASK_TYPES } from '@/services/task.service';
import { pollTask } from '@/utils/task-poller';
import { typeIcon as repoTypeIcon } from '@/utils/repository-types';
import SnapshotList from '@/components/SnapshotList.vue';

export default {
    name: 'RepositorySnapshots',
    components: { SnapshotList },
    data() {
        return {
            repository: null,
            snapshots: [],
            loading: false,
            syncing: false,
            maintenanceLoading: false,
            searchDialogVisible: false,
            searchPattern: '',
            searchResults: [],
            searching: false,
            searched: false,
            searchError: ''
        };
    },
    methods: {
        repoTypeIcon,
        goBack() { this.$router.go(-1); },
        async fetchSnapshots() {
            const id = this.$route.params.id;
            this.loading = true;
            try {
                this.repository = await repositoryService.getRepository(id);
                this.snapshots = await repositoryService.getSnapshots(id);
            } catch (e) {
                Message.error('加载快照失败: ' + e.message);
            } finally {
                this.loading = false;
            }
        },
        // 提交后台任务后轮询到终态并提示；loadingFlag 是本页按钮 loading 字段名。
        // 返回任务是否成功。
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
            const id = this.$route.params.id;
            if (!id) return;
            let res;
            try {
                res = await repositoryService.syncSnapshots(id);
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
            if (ok) await this.fetchSnapshots();
        },
        async handleRestoreSnapshot(data) {
            const { repositoryId, snapshot, targetPath, verify } = data;
            if (!this.repository || !snapshot) return;
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
            if (!this.repository || !snapshot) return;
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
            if (ok) await this.fetchSnapshots();
        },
        async handleBatchDeleteSnapshots(data) {
            const { repositoryId, snapshots } = data;
            if (!this.repository || !snapshots || !snapshots.length) return;
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
            if (ok) await this.fetchSnapshots();
        },
        async handleMaintenance(command) {
            const id = this.$route.params.id;
            const meta = {
                'unlock': { title: '解锁仓库', confirm: '将清理该仓库中陈旧的锁,确定继续吗?', fn: () => repositoryService.unlockRepository(id) },
                'force-unlock': { title: '强制解锁', confirm: '强制解锁会移除仓库中的全部锁,包括其他机器或进程正在持有的锁,可能导致正在进行的备份/删除出错。仅在确认没有其他程序在用该仓库时执行,确定继续吗?', fn: () => repositoryService.unlockRepository(id, true) },
                'prune': { title: '清理未引用数据', confirm: 'prune 会永久删除未被任何快照引用的数据,可能耗时较长,确定继续吗?', fn: () => repositoryService.pruneRepository(id) },
                'deep-check': { title: '深度校验', confirm: '将读取约 5% 的实际数据包进行校验,耗时较长,确定继续吗?', fn: () => repositoryService.deepCheckRepository(id, '5%') },
                'repair-index': { title: '修复索引', confirm: '将重建仓库索引,确定继续吗?', fn: () => repositoryService.repairIndex(id) },
                'repair-snapshots': { title: '修复损坏快照', confirm: '将移除快照中无法读取的数据并生成新快照,确定继续吗?', fn: () => repositoryService.repairSnapshots(id) }
            }[command];
            if (!meta) return;

            try {
                await MessageBox.confirm(meta.confirm, meta.title, { type: 'warning', confirmButtonText: '继续', cancelButtonText: '取消' });
            } catch {
                return;
            }

            let res;
            try {
                res = await meta.fn();
            } catch (e) {
                Message.error(e.response?.data?.message || e.message || '');
                return;
            }
            this.$store.dispatch('tasks/noteSubmitted', res.taskId);
            const ok = await this.awaitTask(res.taskId, { loadingFlag: 'maintenanceLoading', label: meta.title });
            if (ok && (command === 'prune' || command === 'repair-snapshots')) {
                await this.fetchSnapshots();
            }
        },
        openSearch() {
            this.searchDialogVisible = true;
            this.searchPattern = '';
            this.searchResults = [];
            this.searched = false;
            this.searchError = '';
        },
        async performSearch() {
            const pattern = (this.searchPattern || '').trim();
            if (!pattern) {
                Message.warning('请输入搜索模式');
                return;
            }
            let res;
            try {
                res = await repositoryService.findInSnapshots(this.$route.params.id, pattern);
            } catch (e) {
                this.searchError = e.response?.data?.message || e.message || '';
                return;
            }
            this.searching = true;
            this.searchError = '';
            this.$store.dispatch('tasks/noteSubmitted', res.taskId);
            try {
                const task = await pollTask(res.taskId, { shouldStop: () => this._destroyed });
                if (this._destroyed) return;
                this.searched = true;
                if (task.status === 'Succeeded') {
                    this.searchResults = task.resultJson ? JSON.parse(task.resultJson) : [];
                } else {
                    this.searchError = '搜索失败: ' + (task.error || '');
                }
            } catch (e) {
                if (e.message !== 'cancelled') this.searchError = '搜索失败: ' + (e.message || '');
            } finally {
                this.searching = false;
            }
        },
        // 替代旧后端"空缓存自动同步"的体验：检测到空缓存时引导用户发起异步同步
        async maybePromptEmptySync() {
            if (this._emptyPromptShown) return;
            if (!this.repository || !this.repository.isInitialized) return;
            if (this.snapshots.length > 0) return;
            this._emptyPromptShown = true;
            try {
                await MessageBox.confirm(
                    '本地暂无快照缓存，是否立即从远端同步？网盘仓库同步可能耗时较长，期间可继续使用其他页面。',
                    '快照缓存为空',
                    { confirmButtonText: '立即同步', cancelButtonText: '暂不同步', type: 'info' }
                );
            } catch {
                return;
            }
            this.syncSnapshotsFromRemote();
        },
        // 页面重新进入时，恢复本仓库仍在执行的后台任务的轮询（后端重启后任务已标失败，自然拿不到）
        resumeActiveTasks() {
            const repoId = Number(this.$route.params.id);
            const active = this.$store.getters['tasks/activeByRepository'](repoId);
            active.forEach(t => {
                switch (t.type) {
                    case TASK_TYPES.SnapshotSync:
                        this.awaitTask(t.id, { loadingFlag: 'syncing', label: '同步' })
                            .then(ok => { if (ok) this.fetchSnapshots(); });
                        break;
                    case TASK_TYPES.RepositoryUnlock:
                    case TASK_TYPES.RepositoryPrune:
                    case TASK_TYPES.RepositoryDeepCheck:
                    case TASK_TYPES.RepositoryRepairIndex:
                    case TASK_TYPES.RepositoryRepairSnapshots:
                        this.awaitTask(t.id, { loadingFlag: 'maintenanceLoading', label: '运维操作' })
                            .then(ok => {
                                if (ok && (t.type === TASK_TYPES.RepositoryPrune || t.type === TASK_TYPES.RepositoryRepairSnapshots)) {
                                    this.fetchSnapshots();
                                }
                            });
                        break;
                    case TASK_TYPES.SnapshotFind:
                        this.searchDialogVisible = true;
                        this.searching = true;
                        pollTask(t.id, { shouldStop: () => this._destroyed })
                            .then(task => {
                                if (this._destroyed) return;
                                this.searched = true;
                                if (task.status === 'Succeeded') {
                                    this.searchResults = task.resultJson ? JSON.parse(task.resultJson) : [];
                                } else {
                                    this.searchError = '搜索失败: ' + (task.error || '');
                                }
                            })
                            .catch(() => {})
                            .finally(() => { this.searching = false; });
                        break;
                    case TASK_TYPES.SnapshotRestore:
                    case TASK_TYPES.FileRestore:
                    case TASK_TYPES.SnapshotDelete:
                    case TASK_TYPES.SnapshotBatchDelete:
                    case TASK_TYPES.SnapshotTagSet:
                        // 快照级操作在途：完成（或失败）后刷新列表反映最新状态
                        this.awaitTask(t.id, { label: t.title || '操作' })
                            .then(() => this.fetchSnapshots());
                        break;
                    default:
                        break;
                }
            });
        },
        formatSize(bytes) {
            if (!bytes) return '0 B';
            const k = 1024, units = ['B', 'KB', 'MB', 'GB', 'TB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
        }
    },
    async mounted() {
        await this.fetchSnapshots();
        this.maybePromptEmptySync();
        this.resumeActiveTasks();
    },
    beforeDestroy() {
        // 停止本页所有在途任务的轮询
        this._destroyed = true;
    }
};
</script>

<style scoped>
.snapshots-page { padding-bottom: 24px; }
.repo-info { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.repo-info i { margin-right: 4px; }
.page-header__actions { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }

.search-bar { display: flex; gap: 8px; margin-bottom: 12px; }
.search-error { color: var(--danger-500, #f56c6c); font-size: 13px; padding: 12px 0; }
.search-empty { text-align: center; color: var(--color-text-secondary); padding: 24px 0; }
.search-results { max-height: 420px; overflow-y: auto; }
.search-result { border: 1px solid var(--color-border); border-radius: var(--radius-md); padding: 10px 12px; margin-bottom: 10px; }
.search-result__head { display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 6px; }
.search-result__head i { color: var(--brand-600); }
.search-result__file { display: flex; justify-content: space-between; gap: 12px; font-size: 12px; padding: 3px 0; }
.search-result__file .text-mono { word-break: break-all; }
</style>
