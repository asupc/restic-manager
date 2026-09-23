<template>
    <div class="repositories-page">
        <div class="page-header">
            <div>
                <h2 class="page-header__title">备份仓库</h2>
                <div class="page-header__subtitle">管理本地及远程备份仓库,所有任务最终写入此处</div>
            </div>
            <div class="page-header__actions">
                <el-button type="primary" round icon="el-icon-plus" size="small" @click="openCreateDialog">新建仓库</el-button>
            </div>
        </div>

        <el-card shadow="never" v-loading="loading">
            <el-table :data="repositories" style="width: 100%">
                <el-table-column label="名称" min-width="160">
                    <template slot-scope="scope">
                        <div class="repo-cell">
                            <div class="repo-cell__icon"><i class="el-icon-box"></i></div>
                            <div>
                                <div class="repo-cell__name">{{ scope.row.name }}</div>
                                <div class="repo-cell__path">{{ scope.row.path }}</div>
                            </div>
                        </div>
                    </template>
                </el-table-column>
                <el-table-column label="状态" width="120">
                    <template slot-scope="scope">
                        <span class="rm-badge" :class="scope.row.isInitialized ? 'rm-badge--success' : 'rm-badge--warning'">
                            {{ scope.row.isInitialized ? '已初始化' : '未初始化' }}
                        </span>
                    </template>
                </el-table-column>
                <el-table-column label="快照数" width="100" align="center">
                    <template slot-scope="scope">
                        {{ scope.row.stats?.snapshots_count || '-' }}
                    </template>
                </el-table-column>
                <el-table-column label="大小" width="120" align="right">
                    <template slot-scope="scope">
                        {{ scope.row.stats ? formatFileSize(scope.row.stats.total_size) : '-' }}
                    </template>
                </el-table-column>
                <el-table-column label="文件数" width="100" align="right">
                    <template slot-scope="scope">
                        {{ scope.row.stats?.total_file_count || '-' }}
                    </template>
                </el-table-column>
                <el-table-column label="创建时间" width="170">
                    <template slot-scope="scope">
                        <span class="text-mono" style="font-size:12px;">{{ formatDate(scope.row.createdAt) }}</span>
                    </template>
                </el-table-column>
                <el-table-column label="操作" width="620" fixed="right">
                    <template slot-scope="scope">
                        <el-button v-if="scope.row.isInitialized" size="mini" type="text" @click="viewSnapshots(scope.row)">查看快照</el-button>
                        <el-button v-if="scope.row.isInitialized" size="mini" type="text" :loading="scope.row.loadingStats" @click="loadStats(scope.row)">刷新统计</el-button>
                        <template v-if="scope.row.isInitialized">
                            <el-button size="mini" type="text" :loading="scope.row.maintenanceCmd === 'unlock'" :disabled="scope.row.maintenanceLoading" @click="handleMaintenance('unlock', scope.row)">解锁</el-button>
                            <el-button size="mini" type="text" :loading="scope.row.maintenanceCmd === 'force-unlock'" :disabled="scope.row.maintenanceLoading" @click="handleMaintenance('force-unlock', scope.row)">强制解锁</el-button>
                            <el-button size="mini" type="text" :loading="scope.row.maintenanceCmd === 'prune'" :disabled="scope.row.maintenanceLoading" @click="handleMaintenance('prune', scope.row)">prune</el-button>
                            <el-button size="mini" type="text" :loading="scope.row.maintenanceCmd === 'deep-check'" :disabled="scope.row.maintenanceLoading" @click="handleMaintenance('deep-check', scope.row)">深度校验</el-button>
                            <el-button size="mini" type="text" :loading="scope.row.maintenanceCmd === 'repair-index'" :disabled="scope.row.maintenanceLoading" @click="handleMaintenance('repair-index', scope.row)">修复索引</el-button>
                            <el-button size="mini" type="text" :loading="scope.row.maintenanceCmd === 'repair-snapshots'" :disabled="scope.row.maintenanceLoading" @click="handleMaintenance('repair-snapshots', scope.row)">修复快照</el-button>
                        </template>
                        <el-button v-if="!scope.row.isInitialized" size="mini" type="text" :loading="scope.row.initializing" @click="initializeRepository(scope.row)">初始化</el-button>
                        <el-button v-if="!scope.row.isInitialized" size="mini" type="text" :loading="scope.row.validating" @click="validateRepository(scope.row)">验证</el-button>
                        <el-button size="mini" type="text" @click="handleEditRepository(scope.row)">编辑</el-button>
                        <el-button size="mini" type="text" class="text-danger" icon="el-icon-delete" @click="deleteRepository(scope.row)"></el-button>
                    </template>
                </el-table-column>
            </el-table>
        </el-card>

        <!-- 创建/编辑对话框 -->
        <el-dialog :visible.sync="dialogVisible" :title="editingRepository ? '编辑仓库' : '新建仓库'" width="560px" :close-on-click-modal="false" append-to-body>
            <el-form :model="form" :rules="rules" ref="formRef" label-position="top">
                <el-form-item label="仓库名称" prop="name">
                    <el-input v-model="form.name" placeholder="例如: 主服务器备份"></el-input>
                </el-form-item>
                <el-form-item label="仓库路径" prop="path">
                    <folder-selector v-model="form.path" title="选择仓库路径" placeholder="选择或输入仓库存储路径"></folder-selector>
                    <div class="form-tip"><i class="el-icon-info"></i> 仓库数据将存储在此目录下,请确保有足够磁盘空间</div>
                </el-form-item>
                <el-form-item label="仓库密码" prop="password">
                    <el-input v-model="form.password" type="password" show-password placeholder="设置仓库加密密码"></el-input>
                    <div class="form-tip"><i class="el-icon-warning"></i> 密码用于加密所有备份数据,<b>请务必牢记</b>,丢失后将无法恢复数据</div>
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="dialogVisible = false">取消</el-button>
                <el-button type="primary" @click="saveRepository" :loading="saving">{{ editingRepository ? '保存修改' : '创建并初始化' }}</el-button>
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

// 后端持久化的统计结果是 JSON 字符串，解析失败时按未统计处理
function parseStats(json) {
    if (!json) return null;
    try { return JSON.parse(json); } catch (e) { return null; }
}

export default {
    name: 'Repositories',
    components: { FolderSelector },
    data() {
        return {
            loading: false,
            saving: false,
            repositories: [],
            dialogVisible: false,
            editingRepository: null,
            form: { name: '', path: '', password: '', type: 'local' },
            rules: {
                name: [{ required: true, message: '请输入仓库名称', trigger: 'blur' }],
                path: [{ required: true, message: '请输入仓库路径', trigger: 'blur' }],
                password: [{ required: true, message: '请输入仓库密码', trigger: 'blur' }]
            }
        };
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
        async fetchRepositories() {
            this.loading = true;
            try {
                const list = await repositoryService.getRepositories();
                // 统计结果已持久化到仓库行，刷新/重进页面后直接恢复展示
                this.repositories = list.map(repo => ({
                    ...repo,
                    stats: parseStats(repo.statsJson)
                }));
            } catch (e) {
                Message.error('获取仓库列表失败: ' + (e.message || ''));
            } finally {
                this.loading = false;
            }
        },
        // 页面重新进入时，恢复本页仓库上仍在执行的后台任务的轮询
        resumeActiveTasks() {
            const active = this.$store.getters['tasks/activeTasks'];
            if (!active.length) return;
            this.repositories.forEach(repo => {
                active
                    .filter(t => t.repositoryId === repo.id)
                    .forEach(t => {
                        if (t.type === TASK_TYPES.RepositoryInitialize) this.trackTask(t.id, repo, 'initializing', 'init');
                        else if (t.type === TASK_TYPES.RepositoryCheck) this.trackTask(t.id, repo, 'validating', 'check');
                        else if (t.type === TASK_TYPES.RepositoryStats) this.trackTask(t.id, repo, 'loadingStats', 'stats');
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
            this.$set(repo, flag, true);
            try {
                const task = await pollTask(taskId, { shouldStop: () => this._destroyed });
                if (this._destroyed) return;
                const result = task.resultJson ? JSON.parse(task.resultJson) : null;
                if (task.status === 'Succeeded') {
                    if (title) Message.success(title + '完成');
                    else if (kind === 'init') Message.success('仓库初始化成功');
                    else if (kind === 'check') {
                        if (result && result.status === 'Passed') Message.success('仓库结构有效');
                        else Message.error('仓库结构验证失败');
                    } else if (kind === 'stats' && result) {
                        this.$set(repo, 'stats', result);
                        this.$set(repo, 'statsUpdatedAt', task.endTime);
                    }
                } else {
                    const label = title ? title + '失败' : kind === 'init' ? '初始化失败' : kind === 'check' ? '校验失败' : '统计失败';
                    Message.error(label + ': ' + (task.error || ''));
                }
            } catch (e) {
                if (e.message !== 'cancelled') Message.error(e.response?.data?.message || e.message);
            } finally {
                this.$set(repo, flag, false);
            }
        },
        openCreateDialog() {
            this.editingRepository = null;
            this.form = { name: '', path: '', password: '', type: 'local' };
            this.dialogVisible = true;
        },
        async saveRepository() {
            try {
                const valid = await this.$refs.formRef.validate();
                if (!valid) return;
                this.saving = true;
                if (this.editingRepository) {
                    await repositoryService.updateRepository(this.editingRepository.id, this.form);
                    Message.success('仓库已更新');
                } else {
                    await repositoryService.createRepository(this.form);
                    Message.success('仓库创建成功');
                }
                this.dialogVisible = false;
                await this.fetchRepositories();
            } catch (error) {
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
            } catch { return; }
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
                'unlock': { title: '解锁仓库', confirm: '将清理该仓库中陈旧的锁,确定继续吗?', fn: () => repositoryService.unlockRepository(repo.id) },
                'force-unlock': { title: '强制解锁', confirm: '强制解锁会移除仓库中的全部锁,包括其他机器或进程正在持有的锁,可能导致正在进行的备份/删除出错。仅在确认没有其他程序在用该仓库时执行,确定继续吗?', fn: () => repositoryService.unlockRepository(repo.id, true) },
                'prune': { title: '清理未引用数据', confirm: 'prune 会永久删除未被任何快照引用的数据,可能耗时较长,确定继续吗?', fn: () => repositoryService.pruneRepository(repo.id) },
                'deep-check': { title: '深度校验', confirm: '将读取约 5% 的实际数据包进行校验,耗时较长,确定继续吗?', fn: () => repositoryService.deepCheckRepository(repo.id, '5%') },
                'repair-index': { title: '修复索引', confirm: '将重建仓库索引,确定继续吗?', fn: () => repositoryService.repairIndex(repo.id) },
                'repair-snapshots': { title: '修复损坏快照', confirm: '将移除快照中无法读取的数据并生成新快照,确定继续吗?', fn: () => repositoryService.repairSnapshots(repo.id) }
            }[command];
            if (!meta) return;

            try {
                await MessageBox.confirm(meta.confirm, meta.title, { type: 'warning', confirmButtonText: '继续', cancelButtonText: '取消' });
            } catch {
                return;
            }

            // maintenanceCmd 标记当前跑的是哪一项，模板据此只在被点的按钮上转圈
            this.$set(repo, 'maintenanceCmd', command);
            await this.submitAndTrack(repo, 'maintenanceLoading', 'maintenance', meta.fn, meta.title);
            this.$set(repo, 'maintenanceCmd', null);
        },
        viewSnapshots(repo) {
            this.$router.push(`/repositories/${repo.id}/snapshots`);
        },
        handleEditRepository(repo) {
            this.editingRepository = repo;
            this.form = { name: repo.name, path: repo.path, password: '', type: repo.type || 'local' };
            this.dialogVisible = true;
        },
        formatFileSize(b) {
            if (!b) return '0 B';
            const k = 1024, units = ['B', 'KB', 'MB', 'GB', 'TB'];
            const i = Math.floor(Math.log(b) / Math.log(k));
            return parseFloat((b / Math.pow(k, i)).toFixed(2)) + ' ' + units[i];
        },
        formatDate(d) {
            if (!d) return '-';
            try { return new Date(d).toLocaleString('zh-CN', { hour12: false }); }
            catch { return '-'; }
        }
    }
};
</script>

<style scoped>
.repositories-page { padding-bottom: 24px; }

/* 表格 cell */
.repo-cell { display: flex; align-items: center; gap: 10px; }
.repo-cell__icon {
    width: 32px;
    height: 32px;
    border-radius: var(--radius-md);
    background: var(--brand-50);
    color: var(--brand-600);
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
}
.repo-cell__name { font-weight: 600; color: var(--color-text-primary); }
.repo-cell__path {
    font-size: 11px;
    color: var(--color-text-secondary);
    font-family: "SF Mono", Consolas, monospace;
    margin-top: 2px;
    max-width: 240px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

/* 表单 */
.form-tip { margin-top: 6px; font-size: 12px; color: var(--color-text-secondary); }
.form-tip i { margin-right: 4px; }
</style>