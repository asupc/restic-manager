<template>
  <div class="rules-page">
    <div class="page-header">
      <div>
        <h2 class="page-header__title">备份规则</h2>
        <div class="page-header__subtitle">预设包含/排除模式与快照保留策略，可被多个备份任务复用</div>
      </div>
      <div class="page-header__actions">
        <el-button round icon="el-icon-question" size="small" @click="showHelp = true">规则说明</el-button>
        <el-button type="primary" round icon="el-icon-plus" size="small" @click="createRule">新建规则</el-button>
      </div>
    </div>

    <!-- 规则列表 -->
    <div class="rm-surface" v-loading="loading">
      <div v-if="!loading && rules.length === 0" class="empty-state">
        <div class="empty-state__icon"><i class="el-icon-collection-tag"></i></div>
        <div class="empty-state__title">还没有配置备份规则</div>
        <div class="empty-state__desc">通过规则可统一排除构建产物与临时文件，并自动清理过期快照</div>
        <el-button type="primary" size="small" round icon="el-icon-plus" class="mt-3" @click="createRule">创建第一条规则</el-button>
      </div>

      <el-table v-else :data="rules" stripe style="width: 100%">
        <el-table-column label="规则名称" min-width="160">
          <template slot-scope="scope">
            <div class="rule-name-cell">
              <span class="rule-name-cell__icon"><i class="el-icon-collection-tag"></i></span>
              <span class="rule-name">{{ scope.row.name }}</span>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="包含模式" min-width="180" show-overflow-tooltip>
          <template slot-scope="scope">
            <span v-if="scope.row.includePatterns" class="rm-code-chip">
              <span class="rm-code-chip__text">{{ scope.row.includePatterns }}</span>
            </span>
            <span v-else class="text-muted">全部文件</span>
          </template>
        </el-table-column>

        <el-table-column label="排除规则" min-width="210">
          <template slot-scope="scope">
            <div v-if="countPatterns(scope.row.excludePatterns) > 0" class="exclude-cell">
              <span class="exclude-count-badge">
                {{ countPatterns(scope.row.excludePatterns) }} 条排除
              </span>
              <span class="rm-code-chip exclude-preview" :title="scope.row.excludePatterns">
                <span class="rm-code-chip__text">{{ scope.row.excludePatterns }}</span>
              </span>
            </div>
            <span v-else class="text-muted">无排除</span>
          </template>
        </el-table-column>

        <el-table-column label="保留策略" min-width="220">
          <template slot-scope="scope">
            <div class="retention-chips">
              <span v-if="scope.row.keepDaily" class="retention-pill retention-pill--daily">
                日 {{ scope.row.keepDailyCount }}
              </span>
              <span v-if="scope.row.keepWeekly" class="retention-pill retention-pill--weekly">
                周 {{ scope.row.keepWeeklyCount }}
              </span>
              <span v-if="scope.row.keepMonthly" class="retention-pill retention-pill--monthly">
                月 {{ scope.row.keepMonthlyCount }}
              </span>
              <span v-if="scope.row.keepYearly" class="retention-pill retention-pill--yearly">
                年 {{ scope.row.keepYearlyCount }}
              </span>
              <span
                v-if="!scope.row.keepDaily && !scope.row.keepWeekly && !scope.row.keepMonthly && !scope.row.keepYearly"
                class="text-muted"
              >未配置</span>
            </div>
          </template>
        </el-table-column>

        <el-table-column label="关联任务" width="110" align="center">
          <template slot-scope="scope">
            <span
              class="task-count-pill"
              :class="taskCountFor(scope.row.id) > 0 ? 'task-count-pill--active' : ''"
            >
              {{ taskCountFor(scope.row.id) }} 个
            </span>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="130" fixed="right" align="right">
          <template slot-scope="scope">
            <div class="row-actions">
              <el-button size="mini" type="text" icon="el-icon-edit" @click="editRule(scope.row)">编辑</el-button>
              <el-button size="mini" type="text" class="text-danger" icon="el-icon-delete" @click="deleteRule(scope.row)">删除</el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 规则对话框 -->
    <el-dialog
      :title="editingRule ? '编辑备份规则' : '新建备份规则'"
      :visible.sync="dialogVisible"
      width="680px"
      :close-on-click-modal="false"
      append-to-body
    >
      <el-form :model="ruleForm" :rules="formRules" label-position="top" ref="ruleForm">
        <!-- 基础与过滤 -->
        <div class="form-section">
          <div class="form-section__title">
            <i class="el-icon-s-operation"></i> 名称与过滤模式
          </div>
          <el-form-item label="规则名称" prop="name">
            <el-input v-model="ruleForm.name" placeholder="为规则起一个名字，例如: 项目代码排除构建产物"></el-input>
          </el-form-item>

          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="包含模式（可选）">
                <el-input
                  v-model="ruleForm.includePatterns"
                  type="textarea"
                  :rows="4"
                  placeholder="*.docx;*.pdf;./documents/"
                  class="text-mono"
                ></el-input>
                <div class="form-tip"><i class="el-icon-info"></i> 仅备份匹配的文件，留空表示包含全部</div>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="排除模式">
                <el-input
                  v-model="ruleForm.excludePatterns"
                  type="textarea"
                  :rows="4"
                  placeholder="node_modules;.git;*.log;*.tmp"
                  class="text-mono"
                ></el-input>
                <div class="form-tip"><i class="el-icon-info"></i> 多个模式以分号 <code>;</code> 分隔</div>
              </el-form-item>
            </el-col>
          </el-row>

          <!-- 常用排除预设一键追加 -->
          <div class="preset-bar">
            <div class="preset-bar__label">
              <i class="el-icon-magic-stick"></i> 常用排除预设（点击一键去重追加）：
            </div>
            <div class="preset-bar__list">
              <button
                v-for="preset in excludePresets"
                :key="preset"
                type="button"
                class="preset-chip"
                :class="{ 'is-selected': isPresetIncluded(preset) }"
                @click="appendExcludePreset(preset)"
              >
                <i :class="isPresetIncluded(preset) ? 'el-icon-check' : 'el-icon-plus'"></i>
                <span>{{ preset }}</span>
              </button>
            </div>
          </div>
        </div>

        <!-- 保留策略 2x2 卡片网格 -->
        <div class="form-section form-section--last">
          <div class="form-section__title">
            <i class="el-icon-time"></i> 快照保留策略
          </div>
          <div class="retention-grid">
            <div class="retention-card" :class="{ 'retention-card--active': ruleForm.keepDaily }">
              <div class="retention-card__head">
                <div class="retention-card__meta">
                  <span class="retention-pill retention-pill--daily">日</span>
                  <span class="retention-card__title">每日快照</span>
                </div>
                <el-switch v-model="ruleForm.keepDaily"></el-switch>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepDaily }">
                <span>保留最近</span>
                <el-input-number v-model="ruleForm.keepDailyCount" :min="1" :disabled="!ruleForm.keepDaily" size="mini"></el-input-number>
                <span>天</span>
              </div>
            </div>

            <div class="retention-card" :class="{ 'retention-card--active': ruleForm.keepWeekly }">
              <div class="retention-card__head">
                <div class="retention-card__meta">
                  <span class="retention-pill retention-pill--weekly">周</span>
                  <span class="retention-card__title">每周快照</span>
                </div>
                <el-switch v-model="ruleForm.keepWeekly"></el-switch>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepWeekly }">
                <span>保留最近</span>
                <el-input-number v-model="ruleForm.keepWeeklyCount" :min="1" :disabled="!ruleForm.keepWeekly" size="mini"></el-input-number>
                <span>周</span>
              </div>
            </div>

            <div class="retention-card" :class="{ 'retention-card--active': ruleForm.keepMonthly }">
              <div class="retention-card__head">
                <div class="retention-card__meta">
                  <span class="retention-pill retention-pill--monthly">月</span>
                  <span class="retention-card__title">每月快照</span>
                </div>
                <el-switch v-model="ruleForm.keepMonthly"></el-switch>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepMonthly }">
                <span>保留最近</span>
                <el-input-number v-model="ruleForm.keepMonthlyCount" :min="1" :disabled="!ruleForm.keepMonthly" size="mini"></el-input-number>
                <span>个月</span>
              </div>
            </div>

            <div class="retention-card" :class="{ 'retention-card--active': ruleForm.keepYearly }">
              <div class="retention-card__head">
                <div class="retention-card__meta">
                  <span class="retention-pill retention-pill--yearly">年</span>
                  <span class="retention-card__title">每年快照</span>
                </div>
                <el-switch v-model="ruleForm.keepYearly"></el-switch>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepYearly }">
                <span>保留最近</span>
                <el-input-number v-model="ruleForm.keepYearlyCount" :min="1" :disabled="!ruleForm.keepYearly" size="mini"></el-input-number>
                <span>年</span>
              </div>
            </div>
          </div>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveRule" :loading="saving">{{ editingRule ? '保存修改' : '创建规则' }}</el-button>
      </template>
    </el-dialog>

    <!-- 帮助说明 -->
    <el-dialog title="备份规则说明" :visible.sync="showHelp" width="540px" append-to-body>
      <ul class="help-list">
        <li><b>包含模式</b>：仅备份匹配的文件，留空表示包含目录下全部文件。</li>
        <li><b>排除模式</b>：即使被包含，匹配的文件或目录也会被跳过。</li>
        <li>支持通配符：<code>*</code> 单层匹配、<code>**</code> 多层匹配、<code>;</code> 分隔多个模式。</li>
        <li><b>保留策略</b>：每次备份后，超出指定天/周/月/年配额的旧快照将被自动清理。</li>
        <li>一条规则可被多个备份任务引用，修改保存后所有关联任务将立即生效。</li>
      </ul>
    </el-dialog>
  </div>
</template>

<script>
import { backupService } from '@/services/backup.service';
import { backupRuleService } from '@/services/backup-rule.service';

const EXCLUDE_PRESETS = ['node_modules', '.git', '*.log', '*.tmp', 'bin/', 'obj/', '*.cache'];

export default {
  name: 'BackupRules',
  data() {
    return {
      loading: false,
      saving: false,
      showHelp: false,
      rules: [],
      tasks: [],
      excludePresets: EXCLUDE_PRESETS,
      dialogVisible: false,
      editingRule: null,
      ruleForm: this.defaultForm(),
      formRules: {
        name: [{ required: true, message: '请输入规则名称', trigger: 'blur' }]
      }
    };
  },
  computed: {
    ruleTaskCountMap() {
      const map = {};
      this.tasks.forEach(t => {
        if (t.backupRuleId !== null && t.backupRuleId !== undefined) {
          map[t.backupRuleId] = (map[t.backupRuleId] || 0) + 1;
        }
      });
      return map;
    }
  },
  mounted() {
    this.loadRules();
    this.loadTasks();
  },
  methods: {
    defaultForm() {
      return {
        id: 0,
        name: '',
        includePatterns: '',
        excludePatterns: '',
        keepDaily: true,
        keepDailyCount: 7,
        keepWeekly: false,
        keepWeeklyCount: 4,
        keepMonthly: false,
        keepMonthlyCount: 6,
        keepYearly: false,
        keepYearlyCount: 2
      };
    },
    parsePatternList(raw) {
      if (!raw) return [];
      return String(raw)
        .split(/[;\n]+/)
        .map(s => s.trim())
        .filter(Boolean);
    },
    countPatterns(raw) {
      return this.parsePatternList(raw).length;
    },
    isPresetIncluded(preset) {
      return this.parsePatternList(this.ruleForm.excludePatterns).includes(preset);
    },
    appendExcludePreset(preset) {
      const existing = this.parsePatternList(this.ruleForm.excludePatterns);
      if (existing.includes(preset)) {
        this.$message.info(`已包含排除规则 "${preset}"`);
        return;
      }
      existing.push(preset);
      this.ruleForm.excludePatterns = existing.join(';');
    },
    async loadRules() {
      this.loading = true;
      try {
        this.rules = await backupRuleService.getAllRules();
      } catch (e) {
        this.$message.error('加载备份规则失败: ' + (e.response?.data?.message || e.message));
      } finally {
        this.loading = false;
      }
    },
    async loadTasks() {
      try {
        this.tasks = await backupService.getTasks();
      } catch (e) {
        this.$message.error('加载任务失败');
      }
    },
    taskCountFor(ruleId) {
      return this.ruleTaskCountMap[ruleId] || 0;
    },
    createRule() {
      this.editingRule = null;
      this.ruleForm = this.defaultForm();
      this.dialogVisible = true;
      this.$nextTick(() => { this.$refs.ruleForm && this.$refs.ruleForm.clearValidate(); });
    },
    editRule(rule) {
      this.editingRule = rule;
      this.ruleForm = { ...rule };
      this.dialogVisible = true;
      this.$nextTick(() => { this.$refs.ruleForm && this.$refs.ruleForm.clearValidate(); });
    },
    async saveRule() {
      try {
        const valid = await this.$refs.ruleForm.validate();
        if (!valid) return;
      } catch (e) {
        return;
      }
      this.saving = true;
      try {
        if (this.editingRule) {
          await backupRuleService.updateRule(this.editingRule.id, this.ruleForm);
          this.$message.success('规则已更新');
        } else {
          await backupRuleService.createRule(this.ruleForm);
          this.$message.success('规则已创建');
        }
        this.dialogVisible = false;
        await this.loadRules();
      } catch (e) {
        this.$message.error(e.response?.data?.message || e.message);
      } finally {
        this.saving = false;
      }
    },
    async deleteRule(rule) {
      try {
        await this.$confirm(`确定要删除规则 "${rule.name}" 吗？引用该规则的任务将失去过滤配置。`, '删除确认', {
          confirmButtonText: '确定删除',
          cancelButtonText: '取消',
          type: 'warning'
        });
      } catch {
        return;
      }
      try {
        await backupRuleService.deleteRule(rule.id);
        this.$message.success('规则已删除');
        await this.loadRules();
      } catch (e) {
        this.$message.error(e.response?.data?.message || e.message);
      }
    }
  }
};
</script>

<style scoped>
.rules-page {
  padding-bottom: 24px;
}

.rule-name-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.rule-name-cell__icon {
  width: 30px;
  height: 30px;
  border-radius: var(--radius-md);
  background: var(--brand-50);
  color: var(--brand-400);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.rule-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.exclude-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.exclude-count-badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: var(--radius-full);
  background: var(--warning-50);
  color: var(--warning-600);
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
  flex-shrink: 0;
  font-variant-numeric: tabular-nums;
}

.exclude-preview {
  max-width: 170px;
}

/* 保留策略胶囊 */
.retention-chips {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.retention-pill {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: var(--radius-full);
  font-size: 11px;
  font-weight: 600;
  line-height: 1.5;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.retention-pill--daily {
  background: var(--brand-50);
  color: var(--brand-400);
  border: 1px solid var(--brand-200);
}

.retention-pill--weekly {
  background: var(--info-50);
  color: var(--info-600);
  border: 1px solid var(--color-border);
}

.retention-pill--monthly {
  background: var(--success-50);
  color: var(--success-600);
  border: 1px solid var(--color-border);
}

.retention-pill--yearly {
  background: var(--warning-50);
  color: var(--warning-600);
  border: 1px solid var(--color-border);
}

/* 关联任务徽标 */
.task-count-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 44px;
  padding: 2px 10px;
  border-radius: var(--radius-full);
  font-size: 12px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  background: var(--color-bg-soft);
  color: var(--color-text-secondary);
  border: 1px solid var(--color-border);
}

.task-count-pill--active {
  background: var(--brand-50);
  color: var(--brand-400);
  border-color: var(--brand-200);
}

.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  white-space: nowrap;
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

/* 弹窗表单分区 */
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

/* 常用排除预设 */
.preset-bar {
  padding-top: 10px;
  border-top: 1px dashed var(--color-border);
}

.preset-bar__label {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-bottom: 8px;
}

.preset-bar__label i {
  color: var(--brand-400);
  margin-right: 4px;
}

.preset-bar__list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.preset-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  border-radius: var(--radius-full);
  border: 1px solid var(--color-border);
  background: var(--color-bg-card);
  color: var(--color-text-regular);
  font-family: "JetBrains Mono", "SF Mono", Consolas, monospace;
  font-size: 12px;
  cursor: pointer;
  transition: border-color var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast);
}

.preset-chip:hover {
  border-color: var(--brand-200);
  background: var(--brand-50);
  color: var(--brand-300);
}

.preset-chip.is-selected {
  border-color: var(--success-500);
  background: var(--success-50);
  color: var(--success-600);
}

/* 保留策略 2x2 卡片网格 */
.retention-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.retention-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 12px 14px;
  background: var(--color-bg-card);
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.retention-card--active {
  border-color: var(--brand-200);
  box-shadow: var(--shadow-xs);
}

.retention-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 10px;
}

.retention-card__meta {
  display: flex;
  align-items: center;
  gap: 8px;
}

.retention-card__title {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 13px;
}

.retention-card__body {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
  transition: opacity var(--transition-fast);
}

.retention-card__body.is-disabled {
  opacity: 0.42;
}

/* 表单提示 */
.form-tip {
  margin-top: 6px;
  font-size: 12px;
  color: var(--color-text-secondary);
}

.form-tip i {
  margin-right: 4px;
}

.form-tip code {
  padding: 0 4px;
  border-radius: var(--radius-xs);
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  color: var(--brand-400);
}

/* 帮助 */
.help-list {
  padding-left: 20px;
  margin: 0;
}

.help-list li {
  padding: 6px 0;
  color: var(--color-text-regular);
  font-size: 14px;
  line-height: 1.7;
}

.help-list code {
  background: var(--color-bg-soft);
  padding: 1px 6px;
  border-radius: var(--radius-xs);
  font-family: "JetBrains Mono", "SF Mono", Consolas, monospace;
  font-size: 12px;
  color: var(--brand-400);
  border: 1px solid var(--color-border);
}

@media (max-width: 640px) {
  .retention-grid {
    grid-template-columns: 1fr;
  }
}
</style>