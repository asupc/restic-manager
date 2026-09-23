<template>
  <div class="rules-page">
    <div class="page-header">
      <div>
        <h2 class="page-header__title">备份规则</h2>
        <div class="page-header__subtitle">预设包含/排除模式与保留策略,可被多个任务引用</div>
      </div>
      <div class="page-header__actions">
        <el-button round icon="el-icon-question" size="small" @click="showHelp = true">规则说明</el-button>
        <el-button type="primary" round icon="el-icon-plus" size="small" @click="createRule">新增规则</el-button>
      </div>
    </div>

    <el-card shadow="never">
      <el-table :data="rules" style="width: 100%">
        <el-table-column label="名称" min-width="140">
          <template slot-scope="scope">
            <span class="rule-name">{{ scope.row.name }}</span>
          </template>
        </el-table-column>
        <el-table-column label="包含模式" min-width="200" show-overflow-tooltip>
          <template slot-scope="scope">
            <span v-if="scope.row.includePatterns" class="pattern-mono">{{ scope.row.includePatterns }}</span>
            <span v-else class="text-muted">-</span>
          </template>
        </el-table-column>
        <el-table-column label="排除模式" min-width="200" show-overflow-tooltip>
          <template slot-scope="scope">
            <span v-if="scope.row.excludePatterns" class="pattern-mono">{{ scope.row.excludePatterns }}</span>
            <span v-else class="text-muted">-</span>
          </template>
        </el-table-column>
        <el-table-column label="保留策略" min-width="220">
          <template slot-scope="scope">
            <div class="retention-chips">
              <span v-if="scope.row.keepDaily" class="rm-badge rm-badge--info">每日 x {{ scope.row.keepDailyCount }}</span>
              <span v-if="scope.row.keepWeekly" class="rm-badge rm-badge--info">每周 x {{ scope.row.keepWeeklyCount }}</span>
              <span v-if="scope.row.keepMonthly" class="rm-badge rm-badge--info">每月 x {{ scope.row.keepMonthlyCount }}</span>
              <span v-if="scope.row.keepYearly" class="rm-badge rm-badge--info">每年 x {{ scope.row.keepYearlyCount }}</span>
              <span v-if="!scope.row.keepDaily && !scope.row.keepWeekly && !scope.row.keepMonthly && !scope.row.keepYearly" class="text-muted">未配置</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="关联任务" width="90" align="center">
          <template slot-scope="scope">
            {{ taskCountFor(scope.row.id) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="110" fixed="right">
          <template slot-scope="scope">
            <el-button size="mini" type="text" @click="editRule(scope.row)">编辑</el-button>
            <el-button size="mini" type="text" class="text-danger" @click="deleteRule(scope.row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 规则对话框 -->
    <el-dialog :title="editingRule ? '编辑备份规则' : '新建备份规则'" :visible.sync="dialogVisible" width="640px" :close-on-click-modal="false" append-to-body>
      <el-form :model="ruleForm" label-position="top" ref="ruleForm">
        <el-form-item label="规则名称" prop="name">
          <el-input v-model="ruleForm.name" placeholder="为规则起一个名字,例如: 仅文档"></el-input>
        </el-form-item>

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="包含模式">
              <el-input v-model="ruleForm.includePatterns" type="textarea" :rows="3" placeholder="*.docx;*.pdf;./documents/"></el-input>
              <div class="form-tip"><i class="el-icon-info"></i> 匹配这些模式的文件将被包含(留空包含全部)</div>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="排除模式">
              <el-input v-model="ruleForm.excludePatterns" type="textarea" :rows="3" placeholder="*.tmp;**/node_modules;**/logs"></el-input>
              <div class="form-tip"><i class="el-icon-info"></i> 匹配这些模式的文件将被排除</div>
            </el-form-item>
          </el-col>
        </el-row>

        <el-divider><i class="el-icon-time"></i> 保留策略</el-divider>

        <el-row :gutter="16">
          <el-col :span="12">
            <div class="retention-card">
              <div class="retention-card__head">
                <el-switch v-model="ruleForm.keepDaily"></el-switch>
                <span class="retention-card__title">每日备份</span>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepDaily }">
                保留最近
                <el-input-number v-model="ruleForm.keepDailyCount" :min="1" :disabled="!ruleForm.keepDaily" size="small"></el-input-number>
                天的备份
              </div>
            </div>
          </el-col>
          <el-col :span="12">
            <div class="retention-card">
              <div class="retention-card__head">
                <el-switch v-model="ruleForm.keepWeekly"></el-switch>
                <span class="retention-card__title">每周备份</span>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepWeekly }">
                保留最近
                <el-input-number v-model="ruleForm.keepWeeklyCount" :min="1" :disabled="!ruleForm.keepWeekly" size="small"></el-input-number>
                周的备份
              </div>
            </div>
          </el-col>
          <el-col :span="12" class="mt-3">
            <div class="retention-card">
              <div class="retention-card__head">
                <el-switch v-model="ruleForm.keepMonthly"></el-switch>
                <span class="retention-card__title">每月备份</span>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepMonthly }">
                保留最近
                <el-input-number v-model="ruleForm.keepMonthlyCount" :min="1" :disabled="!ruleForm.keepMonthly" size="small"></el-input-number>
                个月的备份
              </div>
            </div>
          </el-col>
          <el-col :span="12" class="mt-3">
            <div class="retention-card">
              <div class="retention-card__head">
                <el-switch v-model="ruleForm.keepYearly"></el-switch>
                <span class="retention-card__title">每年备份</span>
              </div>
              <div class="retention-card__body" :class="{ 'is-disabled': !ruleForm.keepYearly }">
                保留最近
                <el-input-number v-model="ruleForm.keepYearlyCount" :min="1" :disabled="!ruleForm.keepYearly" size="small"></el-input-number>
                年的备份
              </div>
            </div>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveRule" :loading="saving">{{ editingRule ? '保存' : '创建' }}</el-button>
      </template>
    </el-dialog>

    <!-- 帮助 -->
    <el-dialog title="规则说明" :visible.sync="showHelp" width="540px" append-to-body>
      <ul class="help-list">
        <li><b>包含模式</b>:仅备份匹配的文件,留空表示全部文件</li>
        <li><b>排除模式</b>:即使被包含,匹配的文件也会被跳过</li>
        <li>支持通配符:<code>*</code> 单层、<code>**</code> 多层、<code>;</code> 分隔多个模式</li>
        <li><b>保留策略</b>:在每次备份后,旧于指定天/周/月/年的快照将被自动清理</li>
        <li>一个规则可被多个任务引用,修改后所有关联任务立即生效</li>
      </ul>
    </el-dialog>
  </div>
</template>

<script>
import { backupService } from '@/services/backup.service';
import { backupRuleService } from '@/services/backup-rule.service';

export default {
  name: 'BackupRules',
  data() {
    return {
      saving: false,
      showHelp: false,
      rules: [],
      tasks: [],
      dialogVisible: false,
      editingRule: null,
      ruleForm: this.defaultForm()
    };
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
    async loadRules() {
      this.rules = await backupRuleService.getAllRules();
    },
    async loadTasks() {
      try { this.tasks = await backupService.getTasks(); }
      catch (e) { this.$message.error('加载任务失败'); }
    },
    taskCountFor(ruleId) {
      return this.tasks.filter(t => t.backupRuleId === ruleId).length;
    },
    createRule() {
      this.editingRule = null;
      this.ruleForm = this.defaultForm();
      this.dialogVisible = true;
    },
    editRule(rule) {
      this.editingRule = rule;
      this.ruleForm = { ...rule };
      this.dialogVisible = true;
    },
    async saveRule() {
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
        await this.$confirm(`确定要删除规则 "${rule.name}" 吗?引用该规则的任务将失去过滤配置。`, '删除确认', {
          confirmButtonText: '确定删除',
          cancelButtonText: '取消',
          type: 'warning'
        });
      } catch { return; }
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
.rules-page { padding-bottom: 24px; }

/* 表格 */
.rule-name { font-weight: 600; color: var(--color-text-primary); }
.pattern-mono {
  font-family: "SF Mono", Consolas, monospace;
  font-size: 12px;
  color: var(--color-text-regular);
}
.retention-chips { display: flex; gap: 6px; flex-wrap: wrap; }

/* 保留策略卡片 */
.retention-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 12px;
  background: var(--color-bg-card);
}
.retention-card__head { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
.retention-card__title { font-weight: 600; color: var(--color-text-primary); font-size: 14px; }
.retention-card__body {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--color-text-secondary);
  transition: opacity var(--transition-fast);
}
.retention-card__body.is-disabled { opacity: 0.4; }

/* 表单 */
.form-tip { margin-top: 6px; font-size: 12px; color: var(--color-text-secondary); }
.form-tip i { margin-right: 4px; }

/* 帮助 */
.help-list { padding-left: 20px; margin: 0; }
.help-list li { padding: 6px 0; color: var(--color-text-regular); font-size: 14px; line-height: 1.7; }
.help-list code {
  background: var(--color-bg-soft);
  padding: 1px 6px;
  border-radius: 4px;
  font-family: "SF Mono", Consolas, monospace;
  font-size: 12px;
  color: var(--brand-700);
}
</style>