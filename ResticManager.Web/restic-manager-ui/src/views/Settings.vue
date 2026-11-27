<template>
    <div class="settings-page">
        <div class="page-header">
            <div>
                <h2 class="page-header__title">系统设置</h2>
                <div class="page-header__subtitle">个性化控制台主题外观，并配置全仓库自动化维护计划</div>
            </div>
        </div>

        <!-- 外观与偏好卡片 -->
        <div class="rm-surface settings-section">
            <div class="settings-section__head">
                <div class="settings-section__title-wrap">
                    <span class="settings-section__icon settings-section__icon--brand">
                        <i class="el-icon-brush"></i>
                    </span>
                    <div>
                        <h3 class="settings-section__title">外观与偏好</h3>
                        <p class="settings-section__desc">选择控制台界面配色方案，将即时同步至顶部导航栏并持久化保存在本地浏览器</p>
                    </div>
                </div>
                <span class="rm-status-pill rm-status-pill--info">
                    <span class="rm-status-pill__dot"></span>
                    当前：{{ currentTheme === 'light' ? '浅色 Daylight' : '深色 Obsidian' }}
                </span>
            </div>

            <div class="theme-picker-grid">
                <button
                    type="button"
                    class="theme-option"
                    :class="{ 'is-active': currentTheme === 'dark' }"
                    @click="setTheme('dark')"
                >
                    <div class="theme-preview theme-preview--dark">
                        <div class="theme-preview__bar">
                            <span class="theme-preview__dot"></span>
                            <span class="theme-preview__dot"></span>
                            <span class="theme-preview__dot"></span>
                        </div>
                        <div class="theme-preview__body">
                            <div class="theme-preview__side"></div>
                            <div class="theme-preview__main">
                                <div class="theme-preview__line theme-preview__line--accent"></div>
                                <div class="theme-preview__cards">
                                    <span></span>
                                    <span></span>
                                    <span></span>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="theme-option__footer">
                        <div>
                            <div class="theme-option__name">
                                <i class="el-icon-moon"></i>
                                深色 Obsidian
                            </div>
                            <div class="theme-option__desc">低眩光深空炭黑，适合长时间夜间运维</div>
                        </div>
                        <span v-if="currentTheme === 'dark'" class="theme-option__check">
                            <i class="el-icon-check"></i>
                        </span>
                    </div>
                </button>

                <button
                    type="button"
                    class="theme-option"
                    :class="{ 'is-active': currentTheme === 'light' }"
                    @click="setTheme('light')"
                >
                    <div class="theme-preview theme-preview--light">
                        <div class="theme-preview__bar">
                            <span class="theme-preview__dot"></span>
                            <span class="theme-preview__dot"></span>
                            <span class="theme-preview__dot"></span>
                        </div>
                        <div class="theme-preview__body">
                            <div class="theme-preview__side"></div>
                            <div class="theme-preview__main">
                                <div class="theme-preview__line theme-preview__line--accent"></div>
                                <div class="theme-preview__cards">
                                    <span></span>
                                    <span></span>
                                    <span></span>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="theme-option__footer">
                        <div>
                            <div class="theme-option__name">
                                <i class="el-icon-sunny"></i>
                                浅色 Daylight
                            </div>
                            <div class="theme-option__desc">通透明亮日光层级，适合日间办公与展示</div>
                        </div>
                        <span v-if="currentTheme === 'light'" class="theme-option__check">
                            <i class="el-icon-check"></i>
                        </span>
                    </div>
                </button>
            </div>
        </div>

        <!-- 维护计划卡片列表 -->
        <div class="rm-surface settings-section" v-loading="loading">
            <div class="settings-section__head">
                <div class="settings-section__title-wrap">
                    <span class="settings-section__icon settings-section__icon--info">
                        <i class="el-icon-time"></i>
                    </span>
                    <div>
                        <h3 class="settings-section__title">自动化维护计划</h3>
                        <p class="settings-section__desc">开启的项目到点后将自动对全部已初始化仓库执行，任务进度可在侧边栏「后台任务」查看</p>
                    </div>
                </div>
                <span class="rm-status-pill rm-status-pill--success">
                    <span class="rm-status-pill__dot"></span>
                    已启用 <span class="text-mono">{{ enabledCount }}</span> / <span class="text-mono">{{ settings.length }}</span> 项
                </span>
            </div>

            <div class="maintenance-notice">
                <i class="el-icon-info maintenance-notice__icon"></i>
                <div class="maintenance-notice__text">
                    <strong>调度说明：</strong>写操作类任务（清理未引用数据 / 修复索引 / 修复快照）建议错开常规备份高峰时段执行；计划采用 6 段 Quartz Cron 格式（秒 分 时 日 月 周），点击下方快捷标签可一键填充。
                </div>
            </div>

            <div v-if="!loading && settings.length === 0" class="maintenance-empty">
                <i class="el-icon-setting maintenance-empty__icon"></i>
                <div class="maintenance-empty__title">暂无可用的维护计划项</div>
            </div>

            <div v-else class="maintenance-list">
                <div
                    v-for="row in settings"
                    :key="row.id || row.type"
                    class="maintenance-card"
                    :class="{ 'is-enabled': row.isEnabled }"
                >
                    <div class="maintenance-card__header">
                        <div class="maintenance-card__info">
                            <div class="maintenance-card__title-row">
                                <span class="maintenance-card__name">
                                    {{ getMeta(row.type).name }}
                                </span>
                                <span
                                    class="rm-status-pill"
                                    :class="getMeta(row.type).writeOp ? 'rm-status-pill--warning' : 'rm-status-pill--info'"
                                >
                                    {{ getMeta(row.type).writeOp ? '写操作维护' : '只读 / 同步' }}
                                </span>
                                <span
                                    class="rm-status-pill"
                                    :class="row.isEnabled ? 'rm-status-pill--success' : 'rm-status-pill--neutral'"
                                >
                                    <span class="rm-status-pill__dot"></span>
                                    {{ row.isEnabled ? '已启用' : '已停用' }}
                                </span>
                            </div>
                            <div class="maintenance-card__desc">
                                {{ getMeta(row.type).desc }}
                            </div>
                        </div>

                        <div class="maintenance-card__switch">
                            <el-switch
                                :value="row.isEnabled"
                                :disabled="row.saving"
                                @change="(val) => toggleSetting(row, val)"
                            ></el-switch>
                        </div>
                    </div>

                    <div class="maintenance-card__body">
                        <div class="cron-editor">
                            <div class="cron-editor__label">Quartz Cron 表达式（6 段）</div>
                            <div class="cron-row">
                                <el-input
                                    v-model="row.cronEdit"
                                    size="small"
                                    placeholder="如 0 0 2 * * ?"
                                    prefix-icon="el-icon-timer"
                                    class="cron-input text-mono"
                                    :disabled="row.saving"
                                ></el-input>
                                <el-button
                                    size="small"
                                    type="primary"
                                    plain
                                    icon="el-icon-check"
                                    :loading="row.saving"
                                    :disabled="row.saving || row.cronEdit === row.cronExpression"
                                    @click="saveCron(row)"
                                >
                                    保存计划
                                </el-button>
                            </div>
                            <div class="cron-quick-row">
                                <span class="cron-quick-row__hint">快捷填充：</span>
                                <button
                                    v-for="preset in cronPresets"
                                    :key="preset.cron"
                                    type="button"
                                    class="cron-preset-chip"
                                    :class="{ 'is-selected': row.cronEdit === preset.cron }"
                                    :disabled="row.saving"
                                    @click="applyCronPreset(row, preset.cron)"
                                >
                                    <span>{{ preset.label }}</span>
                                    <code class="text-mono">{{ preset.cron }}</code>
                                </button>
                            </div>
                        </div>

                        <div class="maintenance-card__times">
                            <div class="time-metric">
                                <span class="time-metric__label">下次执行</span>
                                <span v-if="row.isEnabled" class="time-metric__val text-mono">
                                    {{ formatDate(row.nextFireTime) }}
                                </span>
                                <span v-else class="time-metric__val text-muted">未启用</span>
                            </div>
                            <div class="time-metric">
                                <span class="time-metric__label">最近更新</span>
                                <span class="time-metric__val text-mono">{{ formatDate(row.updatedAt) }}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script>
import { Message } from 'element-ui';
import { maintenanceService } from '@/services/maintenance.service';

const THEME_STORAGE_KEY = 'rm_theme';

// 维护类型的展示信息（与后端 MaintenanceDefinitions 一一对应）
const typeMeta = {
    SnapshotSync: {
        name: '快照列表同步',
        desc: '从远端仓库定期拉取最新快照元数据，保持本地快照列表与远端一致',
        writeOp: false
    },
    RepositoryStats: {
        name: '刷新仓库统计',
        desc: '执行 restic stats 汇总快照数、文件总数与占用空间并更新缓存',
        writeOp: false
    },
    RepositoryPrune: {
        name: '清理未引用数据（Prune）',
        desc: '永久回收未被任何快照引用的冗余数据包以释放存储空间，耗时较长',
        writeOp: true
    },
    RepositoryDeepCheck: {
        name: '深度完整性校验',
        desc: '随机读取约 5% 实际数据包进行校验，提前发现存储介质静默损坏',
        writeOp: false
    },
    RepositoryRepairIndex: {
        name: '修复仓库索引',
        desc: '扫描数据包并重建仓库索引文件，用于修复异常中断导致的索引偏差',
        writeOp: true
    },
    RepositoryRepairSnapshots: {
        name: '修复损坏快照',
        desc: '移除损坏快照中不可读的数据引用并生成可用新快照',
        writeOp: true
    }
};

const CRON_PRESETS = [
    { label: '每天 02:00', cron: '0 0 2 * * ?' },
    { label: '每天 04:00', cron: '0 0 4 * * ?' },
    { label: '每周日 03:00', cron: '0 0 3 ? * SUN' },
    { label: '每月1日 04:00', cron: '0 0 4 1 * ?' }
];

function normalizeTheme(theme) {
    return theme === 'light' ? 'light' : 'dark';
}

export default {
    name: 'Settings',
    data() {
        return {
            loading: false,
            settings: [],
            typeMeta,
            cronPresets: CRON_PRESETS,
            currentTheme: 'dark'
        };
    },
    computed: {
        enabledCount() {
            return this.settings.filter(s => s && s.isEnabled).length;
        }
    },
    async mounted() {
        const stored = localStorage.getItem(THEME_STORAGE_KEY);
        const attrTheme = document.documentElement.getAttribute('data-theme');
        this.currentTheme = normalizeTheme(stored || attrTheme || 'dark');
        this.$root.$on('theme-changed', this.handleExternalThemeChange);
        await this.loadSettings();
    },
    beforeDestroy() {
        this.$root.$off('theme-changed', this.handleExternalThemeChange);
    },
    methods: {
        getMeta(type) {
            return this.typeMeta[type] || { name: type, desc: '', writeOp: false };
        },
        handleExternalThemeChange(theme) {
            this.currentTheme = normalizeTheme(theme);
        },
        setTheme(theme) {
            const normalized = normalizeTheme(theme);
            if (this.currentTheme === normalized) return;
            this.currentTheme = normalized;
            localStorage.setItem(THEME_STORAGE_KEY, normalized);
            document.documentElement.setAttribute('data-theme', normalized);
            this.$root.$emit('theme-changed', normalized);
        },
        applyCronPreset(row, cron) {
            if (row.saving) return;
            row.cronEdit = cron;
        },
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
            await this.updateSetting(row, { isEnabled: val, cronExpression: row.cronExpression });
        },
        async saveCron(row) {
            await this.updateSetting(row, { isEnabled: row.isEnabled, cronExpression: row.cronEdit });
        },
        async updateSetting(row, payload) {
            if (row.saving) return;
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
.settings-page {
    padding-bottom: var(--space-8);
}

.settings-section {
    padding: 20px 22px;
    margin-bottom: var(--space-5);
}

.settings-section__head {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: var(--space-3);
    margin-bottom: 18px;
}

.settings-section__title-wrap {
    display: flex;
    align-items: flex-start;
    gap: 12px;
}

.settings-section__icon {
    width: 38px;
    height: 38px;
    border-radius: var(--radius-md);
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
    flex-shrink: 0;
}

.settings-section__icon--brand {
    background: var(--brand-50);
    color: var(--brand-400);
    border: 1px solid var(--brand-200);
}

.settings-section__icon--info {
    background: var(--info-50);
    color: var(--info-500);
    border: 1px solid var(--brand-200);
}

.settings-section__title {
    margin: 0;
    font-size: var(--font-lg);
    font-weight: 700;
    color: var(--color-text-primary);
}

.settings-section__desc {
    margin: 4px 0 0;
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
}

/* 主题切换卡片网格 */
.theme-picker-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
    gap: var(--space-4);
}

.theme-option {
    text-align: left;
    padding: 14px;
    border-radius: var(--radius-lg);
    border: 1.5px solid var(--color-border);
    background: var(--color-bg-soft);
    cursor: pointer;
    transition: border-color var(--transition-fast), transform var(--transition-fast), box-shadow var(--transition-fast), background-color var(--transition-fast);
}

.theme-option:hover {
    border-color: var(--brand-200);
    background: var(--color-bg-hover);
    transform: translateY(-2px);
}

.theme-option.is-active {
    border-color: var(--brand-500);
    background: var(--brand-50);
    box-shadow: var(--shadow-sm);
}

.theme-preview {
    height: 92px;
    border-radius: var(--radius-md);
    overflow: hidden;
    border: 1px solid var(--color-border);
    display: flex;
    flex-direction: column;
    margin-bottom: 12px;
}

.theme-preview--dark {
    background: var(--terminal-bg);
    border-color: var(--terminal-border);
}

.theme-preview--light {
    background: var(--color-text-on-brand);
    border-color: var(--color-border-strong);
}

.theme-preview__bar {
    height: 20px;
    padding: 0 8px;
    display: flex;
    align-items: center;
    gap: 5px;
    border-bottom: 1px solid var(--color-border-soft);
}

.theme-preview--dark .theme-preview__bar {
    background: var(--terminal-header);
}

.theme-preview--light .theme-preview__bar {
    background: var(--brand-50);
}

.theme-preview__dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--brand-400);
    opacity: 0.65;
}

.theme-preview__body {
    flex: 1;
    display: flex;
    gap: 8px;
    padding: 8px;
}

.theme-preview__side {
    width: 24%;
    border-radius: var(--radius-xs);
    background: var(--brand-100);
}

.theme-preview__main {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.theme-preview__line {
    height: 10px;
    width: 55%;
    border-radius: var(--radius-xs);
}

.theme-preview__line--accent {
    background: linear-gradient(90deg, var(--brand-500), var(--info-500));
}

.theme-preview__cards {
    flex: 1;
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 6px;
}

.theme-preview__cards span {
    border-radius: var(--radius-xs);
    background: var(--brand-50);
    border: 1px solid var(--brand-200);
}

.theme-option__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
}

.theme-option__name {
    font-size: var(--font-base);
    font-weight: 600;
    color: var(--color-text-primary);
    display: flex;
    align-items: center;
    gap: 6px;
}

.theme-option__desc {
    margin-top: 3px;
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
}

.theme-option__check {
    width: 24px;
    height: 24px;
    border-radius: 50%;
    background: var(--brand-600);
    color: var(--color-text-on-brand);
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-size: 13px;
    flex-shrink: 0;
}

/* 维护说明条 */
.maintenance-notice {
    display: flex;
    align-items: flex-start;
    gap: 10px;
    padding: 12px 14px;
    margin-bottom: 18px;
    border-radius: var(--radius-md);
    background: var(--info-50);
    border: 1px solid var(--brand-200);
    font-size: var(--font-xs);
    color: var(--color-text-regular);
    line-height: 1.6;
}

.maintenance-notice__icon {
    font-size: 16px;
    color: var(--info-500);
    margin-top: 1px;
    flex-shrink: 0;
}

.maintenance-empty {
    text-align: center;
    padding: 48px 16px;
    color: var(--color-text-secondary);
}

.maintenance-empty__icon {
    font-size: 32px;
    color: var(--color-text-placeholder);
    margin-bottom: 8px;
}

.maintenance-empty__title {
    font-size: var(--font-base);
    font-weight: 600;
    color: var(--color-text-primary);
}

/* 维护计划卡片列表 */
.maintenance-list {
    display: flex;
    flex-direction: column;
    gap: 14px;
}

.maintenance-card {
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-soft);
    padding: 16px 18px;
    transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.maintenance-card:hover {
    border-color: var(--color-border-strong);
    box-shadow: var(--shadow-xs);
}

.maintenance-card.is-enabled {
    border-color: var(--brand-200);
}

.maintenance-card__header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: var(--space-4);
    padding-bottom: 12px;
    border-bottom: 1px solid var(--color-border-soft);
}

.maintenance-card__info {
    flex: 1;
    min-width: 0;
}

.maintenance-card__title-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
}

.maintenance-card__name {
    font-size: var(--font-md);
    font-weight: 600;
    color: var(--color-text-primary);
}

.maintenance-card__desc {
    margin-top: 5px;
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
}

.maintenance-card__switch {
    flex-shrink: 0;
    padding-top: 2px;
}

.maintenance-card__body {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: var(--space-4);
    padding-top: 12px;
}

.cron-editor {
    flex: 1;
    min-width: 280px;
}

.cron-editor__label {
    font-size: var(--font-xs);
    font-weight: 600;
    color: var(--color-text-secondary);
    margin-bottom: 6px;
}

.cron-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
}

.cron-input {
    width: 240px;
    max-width: 100%;
}

.cron-quick-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    margin-top: 10px;
}

.cron-quick-row__hint {
    font-size: var(--font-xs);
    color: var(--color-text-placeholder);
}

.cron-preset-chip {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 3px 9px;
    border-radius: var(--radius-sm);
    border: 1px solid var(--color-border);
    background: var(--color-bg-card);
    color: var(--color-text-regular);
    font-size: 11.5px;
    cursor: pointer;
    transition: border-color var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast);
}

.cron-preset-chip code {
    font-size: 11px;
    color: var(--color-text-secondary);
}

.cron-preset-chip:hover:not(:disabled) {
    border-color: var(--brand-200);
    background: var(--color-bg-hover);
    color: var(--brand-300);
}

.cron-preset-chip.is-selected {
    border-color: var(--brand-500);
    background: var(--brand-50);
    color: var(--brand-300);
}

.cron-preset-chip.is-selected code {
    color: var(--brand-300);
}

.cron-preset-chip:disabled {
    opacity: 0.55;
    cursor: not-allowed;
}

.maintenance-card__times {
    display: flex;
    flex-direction: column;
    gap: 8px;
    min-width: 200px;
    padding: 10px 14px;
    border-radius: var(--radius-md);
    background: var(--color-bg-card);
    border: 1px solid var(--color-border-soft);
}

.time-metric {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    font-size: var(--font-xs);
}

.time-metric__label {
    color: var(--color-text-secondary);
}

.time-metric__val {
    font-size: 12px;
    color: var(--color-text-primary);
}

@media (max-width: 768px) {
    .settings-section {
        padding: 16px;
    }
    .maintenance-card__times {
        width: 100%;
    }
}
</style>
