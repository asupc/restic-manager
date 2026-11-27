<template>
    <div class="snapshots-page">
        <!-- 仓库摘要横幅卡 -->
        <div class="rm-surface repo-hero">
            <div class="repo-hero__main">
                <div class="repo-hero__top">
                    <el-button
                        class="repo-hero__back"
                        size="small"
                        icon="el-icon-arrow-left"
                        @click="goBack"
                    >
                        返回
                    </el-button>
                    <h2 class="repo-hero__title">
                        {{ repository && repository.name ? repository.name : '仓库快照' }}
                    </h2>
                    <span v-if="repository" class="repo-type-badge">
                        <i :class="repoTypeIcon(repository.type)"></i>
                        {{ repoTypeLabel(repository.type) }}
                    </span>
                    <span
                        v-if="repository"
                        class="rm-status-pill"
                        :class="repository.isInitialized ? 'rm-status-pill--success' : 'rm-status-pill--warning'"
                    >
                        <span class="rm-status-pill__dot"></span>
                        {{ repository.isInitialized ? '已初始化 · 健康' : '未初始化' }}
                    </span>
                </div>

                <div v-if="repository && repository.path" class="repo-hero__path-row">
                    <span class="repo-hero__path-label">仓库位置</span>
                    <span class="rm-code-chip repo-hero__path-chip" :title="repository.path">
                        <i :class="repoTypeIcon(repository.type)"></i>
                        <span class="rm-code-chip__text">{{ repository.path }}</span>
                        <button
                            type="button"
                            class="rm-code-chip__copy"
                            title="复制仓库路径"
                            @click="copyRepoPath"
                        >
                            <i class="el-icon-document-copy"></i>
                        </button>
                    </span>
                </div>
            </div>

            <div class="repo-hero__actions">
                <el-button
                    type="primary"
                    size="small"
                    icon="el-icon-download"
                    :loading="syncing"
                    :disabled="!repository || !repository.isInitialized"
                    @click="syncSnapshotsFromRemote"
                >
                    同步快照
                </el-button>
                <el-button
                    size="small"
                    icon="el-icon-search"
                    :disabled="!repository || !repository.isInitialized"
                    @click="openSearch"
                >
                    搜索文件
                </el-button>
                <el-button
                    size="small"
                    icon="el-icon-refresh"
                    :loading="loading"
                    @click="fetchSnapshots"
                >
                    刷新
                </el-button>
                <el-dropdown trigger="click" @command="handleMaintenance" :disabled="maintenanceLoading">
                    <el-button size="small" icon="el-icon-setting" :loading="maintenanceLoading">
                        更多运维<i class="el-icon-arrow-down el-icon--right"></i>
                    </el-button>
                    <el-dropdown-menu slot="dropdown">
                        <el-dropdown-item command="unlock" icon="el-icon-unlock">解锁（清理陈旧锁）</el-dropdown-item>
                        <el-dropdown-item command="force-unlock" icon="el-icon-warning-outline">强制解锁（移除所有锁）</el-dropdown-item>
                        <el-dropdown-item command="prune" icon="el-icon-delete" divided>清理未引用数据（prune）</el-dropdown-item>
                        <el-dropdown-item command="deep-check" icon="el-icon-cpu">深度校验（读 5% 数据）</el-dropdown-item>
                        <el-dropdown-item command="repair-index" icon="el-icon-refresh" divided>修复索引</el-dropdown-item>
                        <el-dropdown-item command="repair-snapshots" icon="el-icon-first-aid-kit">修复损坏快照</el-dropdown-item>
                    </el-dropdown-menu>
                </el-dropdown>
            </div>
        </div>

        <!-- 紧凑 KPI 指标卡片网格 -->
        <div class="rm-kpi-grid repo-kpi-grid">
            <div class="rm-kpi-card rm-kpi-card--brand">
                <div class="rm-kpi-card__label">
                    <span>快照总数</span>
                    <i class="el-icon-camera repo-kpi__icon"></i>
                </div>
                <div class="rm-kpi-card__value">{{ snapshotCountDisplay }}</div>
                <div class="rm-kpi-card__meta">
                    <span v-if="repoStats && repoStats.snapshots_count !== undefined && repoStats.snapshots_count !== null">
                        远端统计 {{ formatNumber(repoStats.snapshots_count) }} 个
                    </span>
                    <span v-else>当前本地缓存快照数</span>
                </div>
            </div>

            <div class="rm-kpi-card rm-kpi-card--info">
                <div class="rm-kpi-card__label">
                    <span>文件总数</span>
                    <i class="el-icon-document repo-kpi__icon"></i>
                </div>
                <div class="rm-kpi-card__value">{{ totalFileCountDisplay }}</div>
                <div class="rm-kpi-card__meta">来自最近一次仓库统计</div>
            </div>

            <div class="rm-kpi-card rm-kpi-card--success">
                <div class="rm-kpi-card__label">
                    <span>数据总量</span>
                    <i class="el-icon-coin repo-kpi__icon"></i>
                </div>
                <div class="rm-kpi-card__value">{{ totalSizeDisplay }}</div>
                <div class="rm-kpi-card__meta">去重压缩后仓库逻辑规模</div>
            </div>

            <div class="rm-kpi-card rm-kpi-card--purple">
                <div class="rm-kpi-card__label">
                    <span>最近检查/统计</span>
                    <i class="el-icon-time repo-kpi__icon"></i>
                </div>
                <div class="rm-kpi-card__value repo-kpi__value--date">{{ lastStatsTimeDisplay }}</div>
                <div class="rm-kpi-card__meta">
                    {{ repository && repository.statsUpdatedAt ? '统计结果已持久化缓存' : '可在仓库页或维护计划触发统计' }}
                </div>
            </div>
        </div>

        <!-- 快照列表 -->
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
        <el-dialog
            :visible.sync="searchDialogVisible"
            title="跨快照搜索文件"
            width="680px"
            :close-on-click-modal="false"
            append-to-body
        >
            <div class="search-bar">
                <el-input
                    v-model="searchPattern"
                    placeholder="输入文件名或通配符，例如 *.docx、config.json"
                    prefix-icon="el-icon-search"
                    clearable
                    @keyup.enter.native="performSearch"
                ></el-input>
                <el-button type="primary" icon="el-icon-search" :loading="searching" @click="performSearch">
                    搜索
                </el-button>
            </div>
            <div class="search-tips">
                支持通配符匹配（<span class="text-mono">*</span>、<span class="text-mono">?</span>），将在该仓库的全部历史快照中检索文件路径。
            </div>

            <el-alert
                v-if="searchError"
                type="error"
                :title="searchError"
                :closable="false"
                show-icon
                class="search-alert"
            ></el-alert>

            <div v-else-if="searching" class="search-state">
                <i class="el-icon-loading search-state__icon"></i>
                <div class="search-state__title">正在跨快照检索匹配文件...</div>
                <div class="search-state__desc">远端仓库检索可能需要数秒，请稍候</div>
            </div>

            <div v-else-if="searched && searchResults.length === 0" class="search-state">
                <i class="el-icon-document-delete search-state__icon"></i>
                <div class="search-state__title">未找到匹配的文件</div>
                <div class="search-state__desc">尝试更换关键字或使用更宽泛的通配符（如 *keyword*）</div>
            </div>

            <div v-else-if="searchResults.length > 0" class="search-results">
                <div class="search-results__summary">
                    共在 <span class="text-mono text-bold">{{ searchResults.length }}</span> 个快照中找到匹配项
                </div>
                <div v-for="r in searchResults" :key="r.snapshot" class="search-result">
                    <div class="search-result__head">
                        <span class="rm-code-chip">
                            <i class="el-icon-time"></i>
                            <span>{{ (r.snapshot || '').substring(0, 8) }}</span>
                        </span>
                        <span class="rm-status-pill rm-status-pill--info">
                            {{ r.hits || (r.matches ? r.matches.length : 0) }} 处命中
                        </span>
                    </div>
                    <div class="search-result__files">
                        <div v-for="(m, i) in r.matches" :key="i" class="search-result__file">
                            <span class="search-result__path text-mono" :title="m.path">
                                <i class="el-icon-document"></i>
                                {{ m.path }}
                            </span>
                            <span class="search-result__size text-mono text-secondary">{{ formatSize(m.size) }}</span>
                        </div>
                    </div>
                </div>
            </div>

            <div v-else class="search-state search-state--idle">
                <i class="el-icon-search search-state__icon"></i>
                <div class="search-state__title">输入文件名开始搜索</div>
                <div class="search-state__desc">快速定位指定文件分布在哪些历史快照版本中</div>
            </div>
        </el-dialog>
    </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui';
import { repositoryService } from '@/services/repository.service';
import { TASK_TYPES } from '@/services/task.service';
import { pollTask } from '@/utils/task-poller';
import { REPOSITORY_TYPE_META, typeIcon as repoTypeIcon } from '@/utils/repository-types';
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
    name: 'RepositorySnapshots',
    components: { SnapshotList },
    data() {
        return {
            repository: null,
            snapshots: [],
            loading: false,
            syncing: false,
            maintenanceRunningCount: 0,
            searchDialogVisible: false,
            searchPattern: '',
            searchResults: [],
            searching: false,
            searched: false,
            searchError: ''
        };
    },
    computed: {
        maintenanceLoading() {
            return this.maintenanceRunningCount > 0;
        },
        repoStats() {
            if (!this.repository || !this.repository.statsJson) return null;
            const parsed = safeParseJson(this.repository.statsJson, null);
            return parsed && typeof parsed === 'object' ? parsed : null;
        },
        snapshotCountDisplay() {
            return this.formatNumber(this.snapshots ? this.snapshots.length : 0);
        },
        totalFileCountDisplay() {
            if (!this.repoStats) return '—';
            const val = this.repoStats.total_file_count;
            return val !== undefined && val !== null ? this.formatNumber(val) : '—';
        },
        totalSizeDisplay() {
            if (!this.repoStats) return '—';
            const val = this.repoStats.total_size;
            return val !== undefined && val !== null ? this.formatSize(val) : '—';
        },
        lastStatsTimeDisplay() {
            if (!this.repository || !this.repository.statsUpdatedAt) return '尚未统计';
            return this.formatDate(this.repository.statsUpdatedAt);
        }
    },
    watch: {
        '$route.params.id'(newId, oldId) {
            if (newId === oldId) return;
            this._routeToken += 1;
            this._searchSeq += 1;
            this._trackedTaskIds = new Set();
            this._emptyPromptShown = false;
            this.syncing = false;
            this.maintenanceRunningCount = 0;
            this.searching = false;
            this.searchDialogVisible = false;
            this.searchResults = [];
            this.searched = false;
            this.searchError = '';
            this.repository = null;
            this.snapshots = [];
            this.initPage();
        }
    },
    created() {
        this._routeToken = 0;
        this._fetchReqSeq = 0;
        this._searchSeq = 0;
        this._trackedTaskIds = new Set();
    },
    methods: {
        repoTypeIcon,
        repoTypeLabel(type) {
            const meta = REPOSITORY_TYPE_META[type];
            return meta ? meta.label : (type || 'local');
        },
        goBack() { this.$router.go(-1); },
        async copyRepoPath() {
            const text = this.repository && this.repository.path ? this.repository.path : '';
            if (!text) return;
            try {
                await navigator.clipboard.writeText(text);
                Message.success('仓库路径已复制到剪贴板');
            } catch (e) {
                Message.info('请手动选中路径复制');
            }
        },
        async initPage() {
            const token = this._routeToken;
            await this.fetchSnapshots();
            if (this._destroyed || token !== this._routeToken) return;
            this.resumeActiveTasks();
            this.maybePromptEmptySync();
        },
        async fetchSnapshots() {
            const id = this.$route.params.id;
            const token = this._routeToken;
            const reqSeq = ++this._fetchReqSeq;
            this.loading = true;
            try {
                const [repo, snapshots] = await Promise.all([
                    repositoryService.getRepository(id),
                    repositoryService.getSnapshots(id)
                ]);
                if (this._destroyed || token !== this._routeToken || reqSeq !== this._fetchReqSeq) return;
                this.repository = repo;
                this.snapshots = snapshots;
            } catch (e) {
                if (this._destroyed || token !== this._routeToken || reqSeq !== this._fetchReqSeq) return;
                Message.error('加载快照失败: ' + e.message);
            } finally {
                if (!this._destroyed && token === this._routeToken && reqSeq === this._fetchReqSeq) {
                    this.loading = false;
                }
            }
        },
        // 提交后台任务后轮询到终态并提示；loadingFlag 是本页按钮 loading 字段名。
        // 返回任务是否成功。
        async awaitTask(taskId, { loadingFlag = null, label = '操作', successMessage = null, isMaintenance = false, token = this._routeToken } = {}) {
            if (this._trackedTaskIds.has(taskId)) return false;
            this._trackedTaskIds.add(taskId);
            const myToken = token;
            if (isMaintenance) this.maintenanceRunningCount += 1;
            else if (loadingFlag) this[loadingFlag] = true;
            let ok = false;
            try {
                const task = await pollTask(taskId, { shouldStop: () => this._destroyed || myToken !== this._routeToken });
                if (this._destroyed || myToken !== this._routeToken) return false;
                ok = task.status === 'Succeeded';
                if (ok) {
                    Message.success(successMessage ? successMessage(task) : label + '完成');
                } else {
                    Message.error(label + '失败: ' + (task.error || ''));
                }
            } catch (e) {
                if (this._destroyed || myToken !== this._routeToken) return false;
                if (e.message !== 'cancelled') Message.error(label + '失败: ' + (e.message || ''));
            } finally {
                if (myToken === this._routeToken) {
                    this._trackedTaskIds.delete(taskId);
                    if (isMaintenance) this.maintenanceRunningCount = Math.max(0, this.maintenanceRunningCount - 1);
                    else if (loadingFlag) this[loadingFlag] = false;
                }
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
                    const r = safeParseJson(task.resultJson, {});
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
                    const r = safeParseJson(task.resultJson, {});
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
                    const r = safeParseJson(task.resultJson, {});
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
            const ok = await this.awaitTask(res.taskId, { isMaintenance: true, label: meta.title });
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
            const seq = ++this._searchSeq;
            const token = this._routeToken;
            this.searching = true;
            this.searchError = '';
            try {
                let res;
                try {
                    res = await repositoryService.findInSnapshots(this.$route.params.id, pattern);
                } catch (e) {
                    if (this._destroyed || seq !== this._searchSeq || token !== this._routeToken) return;
                    this.searchError = e.response?.data?.message || e.message || '';
                    return;
                }
                if (this._destroyed || seq !== this._searchSeq || token !== this._routeToken) return;
                this.$store.dispatch('tasks/noteSubmitted', res.taskId);
                const task = await pollTask(res.taskId, {
                    shouldStop: () => this._destroyed || seq !== this._searchSeq || token !== this._routeToken
                });
                if (this._destroyed || seq !== this._searchSeq || token !== this._routeToken) return;
                this.searched = true;
                if (task.status === 'Succeeded') {
                    this.searchResults = safeParseJson(task.resultJson, []);
                } else {
                    this.searchError = '搜索失败: ' + (task.error || '');
                }
            } catch (e) {
                if (this._destroyed || seq !== this._searchSeq || token !== this._routeToken) return;
                if (e.message !== 'cancelled') this.searchError = '搜索失败: ' + (e.message || '');
            } finally {
                if (!this._destroyed && seq === this._searchSeq && token === this._routeToken) {
                    this.searching = false;
                }
            }
        },
        // 替代旧后端"空缓存自动同步"的体验：检测到空缓存时引导用户发起异步同步
        async maybePromptEmptySync() {
            if (this._emptyPromptShown) return;
            if (!this.repository || !this.repository.isInitialized) return;
            if (this.snapshots.length > 0) return;
            const repoId = Number(this.$route.params.id);
            const active = this.$store.getters['tasks/activeByRepository'](repoId) || [];
            if (this.syncing || active.some(t => t.type === TASK_TYPES.SnapshotSync)) return;
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
            const token = this._routeToken;
            const active = this.$store.getters['tasks/activeByRepository'](repoId);
            active.forEach(t => {
                if (this._trackedTaskIds.has(t.id)) return;
                switch (t.type) {
                    case TASK_TYPES.SnapshotSync:
                        this.awaitTask(t.id, { loadingFlag: 'syncing', label: '同步', token })
                            .then(ok => {
                                if (ok && !this._destroyed && token === this._routeToken) this.fetchSnapshots();
                            });
                        break;
                    case TASK_TYPES.RepositoryUnlock:
                    case TASK_TYPES.RepositoryPrune:
                    case TASK_TYPES.RepositoryDeepCheck:
                    case TASK_TYPES.RepositoryRepairIndex:
                    case TASK_TYPES.RepositoryRepairSnapshots:
                        this.awaitTask(t.id, { isMaintenance: true, label: '运维操作', token })
                            .then(ok => {
                                if (ok && !this._destroyed && token === this._routeToken && (t.type === TASK_TYPES.RepositoryPrune || t.type === TASK_TYPES.RepositoryRepairSnapshots)) {
                                    this.fetchSnapshots();
                                }
                            });
                        break;
                    case TASK_TYPES.SnapshotFind: {
                        const seq = ++this._searchSeq;
                        this._trackedTaskIds.add(t.id);
                        this.searchDialogVisible = true;
                        this.searching = true;
                        pollTask(t.id, { shouldStop: () => this._destroyed || token !== this._routeToken || seq !== this._searchSeq })
                            .then(task => {
                                if (this._destroyed || token !== this._routeToken || seq !== this._searchSeq) return;
                                this.searched = true;
                                if (task.status === 'Succeeded') {
                                    this.searchResults = safeParseJson(task.resultJson, []);
                                } else {
                                    this.searchError = '搜索失败: ' + (task.error || '');
                                }
                            })
                            .catch(() => {})
                            .finally(() => {
                                if (token === this._routeToken) {
                                    this._trackedTaskIds.delete(t.id);
                                    if (seq === this._searchSeq) this.searching = false;
                                }
                            });
                        break;
                    }
                    case TASK_TYPES.SnapshotRestore:
                    case TASK_TYPES.FileRestore:
                    case TASK_TYPES.SnapshotDelete:
                    case TASK_TYPES.SnapshotBatchDelete:
                    case TASK_TYPES.SnapshotTagSet:
                        // 快照级操作在途：完成（或失败）后刷新列表反映最新状态
                        this.awaitTask(t.id, { label: t.title || '操作', token })
                            .then(() => {
                                if (!this._destroyed && token === this._routeToken) this.fetchSnapshots();
                            });
                        break;
                    default:
                        break;
                }
            });
        },
        formatSize(bytes) {
            if (!bytes) return '0 B';
            const k = 1024;
            const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
            const i = Math.min(Math.floor(Math.log(bytes) / Math.log(k)), units.length - 1);
            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
        },
        formatNumber(val) {
            if (val === null || val === undefined || isNaN(Number(val))) return '—';
            return Number(val).toLocaleString('zh-CN');
        },
        formatDate(value) {
            if (!value) return '—';
            const d = new Date(value);
            if (isNaN(d.getTime())) return String(value);
            const pad = n => String(n).padStart(2, '0');
            return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
        }
    },
    async mounted() {
        await this.initPage();
    },
    beforeDestroy() {
        // 停止本页所有在途任务的轮询
        this._destroyed = true;
        this._routeToken += 1;
    }
};
</script>

<style scoped>
.snapshots-page {
    padding-bottom: var(--space-8);
}

/* 顶部仓库摘要横幅卡 */
.repo-hero {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: var(--space-4);
    padding: 18px 22px;
    margin-bottom: var(--space-5);
    position: relative;
}

.repo-hero::before {
    content: "";
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 2px;
    background: linear-gradient(90deg, var(--brand-500), var(--info-500) 45%, transparent 90%);
    opacity: 0.9;
}

.repo-hero__main {
    flex: 1;
    min-width: 260px;
}

.repo-hero__top {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 10px;
}

.repo-hero__back {
    padding: 6px 12px;
}

.repo-hero__title {
    margin: 0;
    font-size: var(--font-xl);
    font-weight: 700;
    color: var(--color-text-primary);
    letter-spacing: -0.02em;
    line-height: 1.3;
}

.repo-type-badge {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 3px 10px;
    border-radius: var(--radius-full);
    font-size: var(--font-xs);
    font-weight: 600;
    color: var(--brand-300);
    background: var(--brand-50);
    border: 1px solid var(--brand-200);
}

.repo-hero__path-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 12px;
}

.repo-hero__path-label {
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
    flex-shrink: 0;
}

.repo-hero__path-chip {
    max-width: min(100%, 620px);
    padding: 3px 8px 3px 10px;
}

.repo-hero__actions {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--space-2);
    flex-shrink: 0;
}

/* KPI 指标卡微调 */
.repo-kpi-grid {
    margin-bottom: var(--space-5);
}

.repo-kpi__icon {
    font-size: 15px;
    color: var(--color-text-placeholder);
}

.repo-kpi__value--date {
    font-size: var(--font-xl);
    line-height: 1.45;
    padding-top: 4px;
}

/* 跨快照文件搜索弹窗 */
.search-bar {
    display: flex;
    align-items: center;
    gap: var(--space-2);
}

.search-tips {
    margin-top: 8px;
    margin-bottom: 14px;
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
}

.search-alert {
    margin-bottom: 12px;
}

.search-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    text-align: center;
    padding: 40px 20px;
    border: 1px dashed var(--color-border-strong);
    border-radius: var(--radius-md);
    background: var(--color-bg-soft);
}

.search-state__icon {
    font-size: 30px;
    color: var(--brand-400);
    margin-bottom: 10px;
}

.search-state--idle .search-state__icon {
    color: var(--color-text-placeholder);
}

.search-state__title {
    font-size: var(--font-base);
    font-weight: 600;
    color: var(--color-text-primary);
}

.search-state__desc {
    margin-top: 4px;
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
}

.search-results {
    max-height: 430px;
    overflow-y: auto;
    padding-right: 4px;
}

.search-results__summary {
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
    margin-bottom: 10px;
}

.search-result {
    border: 1px solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-bg-soft);
    padding: 12px 14px;
    margin-bottom: 10px;
    transition: border-color var(--transition-fast);
}

.search-result:hover {
    border-color: var(--brand-200);
}

.search-result__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    margin-bottom: 10px;
    padding-bottom: 8px;
    border-bottom: 1px solid var(--color-border-soft);
}

.search-result__files {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.search-result__file {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 4px 6px;
    border-radius: var(--radius-xs);
    font-size: 12px;
}

.search-result__file:hover {
    background: var(--color-bg-hover);
}

.search-result__path {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    color: var(--color-text-regular);
    word-break: break-all;
}

.search-result__path i {
    color: var(--color-text-placeholder);
    flex-shrink: 0;
}

.search-result__size {
    flex-shrink: 0;
    font-size: 12px;
}

@media (max-width: 768px) {
    .repo-hero {
        padding: 14px 16px;
    }
    .repo-hero__actions {
        width: 100%;
    }
}
</style>
