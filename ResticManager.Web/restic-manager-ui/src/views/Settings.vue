<template>
    <div class="settings-page">
        <div class="page-header">
            <div>
                <h2 class="page-header__title">系统设置</h2>
                <div class="page-header__subtitle">按 Cron 计划对全部已初始化仓库批量执行维护操作</div>
            </div>
        </div>

        <el-card shadow="never" v-loading="loading">
            <el-alert type="info" :closable="false" class="mb-3"
                title="开启的项目到点后自动对全部已初始化仓库执行,进度可在侧边栏「后台任务」中查看"
                description="写操作类(清理/修复)建议避开备份时段执行;执行计划使用 6 段 Quartz Cron 格式,如 0 30 4 * * ? 表示每天 04:30。"
                show-icon>
            </el-alert>

            <el-table :data="settings" style="width: 100%">
                <el-table-column label="维护项" min-width="230">
                    <template slot-scope="scope">
                        <div class="setting-name">{{ typeMeta[scope.row.type] ? typeMeta[scope.row.type].name : scope.row.type }}</div>
                        <div class="setting-desc text-muted">{{ typeMeta[scope.row.type] ? typeMeta[scope.row.type].desc : '' }}</div>
                    </template>
                </el-table-column>
                <el-table-column label="状态" width="80" align="center">
                    <template slot-scope="scope">
                        <el-switch :value="scope.row.isEnabled" @change="(val) => toggleSetting(scope.row, val)"></el-switch>
                    </template>
                </el-table-column>
                <el-table-column label="执行计划" min-width="300">
                    <template slot-scope="scope">
                        <div class="cron-row">
                            <el-input v-model="scope.row.cronEdit" size="small" placeholder="6 段 Quartz Cron 表达式" class="cron-input"></el-input>
                            <el-button size="mini" type="text" :loading="scope.row.saving"
                                :disabled="scope.row.cronEdit === scope.row.cronExpression"
                                @click="saveCron(scope.row)">保存</el-button>
                        </div>
                        <div class="form-tip cron-quick-row">
                            <el-tag size="mini" effect="plain" class="cron-quick" @click="scope.row.cronEdit = '0 0 4 * * ?'">每天 4 点</el-tag>
                            <el-tag size="mini" effect="plain" class="cron-quick" @click="scope.row.cronEdit = '0 0 4 ? * SUN'">每周日 4 点</el-tag>
                            <el-tag size="mini" effect="plain" class="cron-quick" @click="scope.row.cronEdit = '0 0 3 1 * ?'">每月 1 日 3 点</el-tag>
                        </div>
                    </template>
                </el-table-column>
                <el-table-column label="下次执行" width="170">
                    <template slot-scope="scope">
                        <span v-if="scope.row.isEnabled" class="text-mono" style="font-size:12px;">{{ formatDate(scope.row.nextFireTime) }}</span>
                        <span v-else class="text-muted">-</span>
                    </template>
                </el-table-column>
                <el-table-column label="更新于" width="170">
                    <template slot-scope="scope">
                        <span class="text-mono" style="font-size:12px;">{{ formatDate(scope.row.updatedAt) }}</span>
                    </template>
                </el-table-column>
            </el-table>
        </el-card>
    </div>
</template>

<script>
import { Message } from 'element-ui';
import { maintenanceService } from '@/services/maintenance.service';

// 维护类型的展示信息（与后端 MaintenanceDefinitions 一一对应）
const typeMeta = {
    SnapshotSync: { name: '快照列表同步', desc: '从远端拉取最新快照元数据,保持快照缓存新鲜' },
    RepositoryStats: { name: '刷新仓库统计', desc: '执行 restic stats,更新快照数/占用/文件数' },
    RepositoryPrune: { name: '清理未引用数据', desc: 'prune,永久删除未被快照引用的数据,耗时较长' },
    RepositoryDeepCheck: { name: '深度校验', desc: '读取约 5% 实际数据包校验完整性,耗时较长' },
    RepositoryRepairIndex: { name: '修复索引', desc: '重建仓库索引文件' },
    RepositoryRepairSnapshots: { name: '修复快照', desc: '移除损坏快照中不可读的数据并生成新快照' }
};

export default {
    name: 'Settings',
    data() {
        return {
            loading: false,
            settings: [],
            typeMeta
        };
    },
    async mounted() {
        await this.loadSettings();
    },
    methods: {
        async loadSettings() {
            this.loading = true;
            try {
                const list = await maintenanceService.getMaintenanceSettings();
                this.settings = list.map(item => this.normalize(item));
            } catch (e) {
                Message.error('获取维护配置失败: ' + (e.response?.data?.message || e.message));
            } finally {
                this.loading = false;
            }
        },
        // 补充编辑副本与保存状态字段
        normalize(item) {
            return { ...item, cronEdit: item.cronExpression, saving: false };
        },
        // 开关即时保存（失败保持原状态,el-switch 为受控值无需回滚）
        async toggleSetting(row, val) {
            await this.updateSetting(row, { isEnabled: val, cronExpression: row.cronEdit });
        },
        async saveCron(row) {
            await this.updateSetting(row, { isEnabled: row.isEnabled, cronExpression: row.cronEdit });
        },
        async updateSetting(row, payload) {
            row.saving = true;
            try {
                const updated = await maintenanceService.updateMaintenanceSetting(row.id, payload);
                Object.assign(row, this.normalize(updated));
                Message.success('维护配置已更新');
            } catch (e) {
                Message.error(e.response?.data?.message || e.message);
            } finally {
                row.saving = false;
            }
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
.settings-page { padding-bottom: 24px; }

.setting-name { font-weight: 600; }

.setting-desc {
    font-size: 12px;
    margin-top: 2px;
}

.cron-row {
    display: flex;
    align-items: center;
    gap: 8px;
}

.cron-input { max-width: 220px; }

.cron-quick-row { margin-top: 4px; }

.cron-quick { cursor: pointer; margin-right: 6px; }
</style>
