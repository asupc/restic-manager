<template>
  <div class="backup-rules-container">
    <div class="page-header">
      <h2>备份规则管理</h2>
      <div>
        <el-button type="primary" size="small" @click="createRule" icon="el-icon-plus">
          新增规则
        </el-button>
      </div>
    </div>

    <el-card>
      <el-table :data="rules" style="width: 100%" v-if="rules.length > 0">
        <el-table-column prop="id" label="ID" width="60"></el-table-column>
        <el-table-column prop="name" label="规则名称" width="180"></el-table-column>
        <el-table-column label="包含模式">
          <template #default="scope">
            {{ scope.row.includePatterns || '无' }}
          </template>
        </el-table-column>
        <el-table-column label="排除模式">
          <template #default="scope">
            {{ scope.row.excludePatterns || '无' }}
          </template>
        </el-table-column>
        <el-table-column label="保留策略">
          <template #default="scope">
            <div>
              <div v-if="scope.row.keepDaily">每日: {{ scope.row.keepDailyCount }}</div>
              <div v-if="scope.row.keepWeekly">每周: {{ scope.row.keepWeeklyCount }}</div>
              <div v-if="scope.row.keepMonthly">每月: {{ scope.row.keepMonthlyCount }}</div>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150">
          <template #default="scope">
            <el-button size="mini" type="primary" @click="editRule(scope.row)">
              编辑
            </el-button>
            <el-button size="mini" type="danger" @click="deleteRule(scope.row)">
              删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-empty v-else description="暂无备份规则"></el-empty>
    </el-card>

    <!-- 规则编辑对话框 -->
    <el-dialog :title="editingRule ? '编辑备份规则' : '新增备份规则'" :visible.sync="dialogVisible" width="600px"
      :close-on-click-modal="false" append-to-body>
      <el-form :model="ruleForm" label-width="120px" ref="ruleForm" label-position="left">
        <el-form-item label="规则名称" prop="name">
          <el-input v-model="ruleForm.name" placeholder="请输入规则名称"></el-input>
        </el-form-item>

        <el-form-item label="包含模式">
          <el-input v-model="ruleForm.includePatterns" placeholder="例如：*.docx;*.pdf;./documents/"></el-input>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            匹配这些模式的文件将被包含（留空表示包含所有）
          </div>
        </el-form-item>

        <el-form-item label="排除模式">
          <el-input v-model="ruleForm.excludePatterns" placeholder="例如：*.tmp;**/node_modules;**/logs"></el-input>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            匹配这些模式的文件将被排除
          </div>
        </el-form-item>

        <el-divider>保留策略</el-divider>

        <el-form-item label="每日备份">
          <div class="strategy-item">
            <el-checkbox v-model="ruleForm.keepDaily">
              启用每日备份保留
            </el-checkbox>
            <el-input-number v-model="ruleForm.keepDailyCount" :min="1" :disabled="!ruleForm.keepDaily"
              style="width: 120px; margin-left: 10px" label="保留数量"></el-input-number>
            <span class="count-label">个</span>
          </div>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            保留最近N天的备份
          </div>
        </el-form-item>

        <el-form-item label="每周备份">
          <div class="strategy-item">
            <el-checkbox v-model="ruleForm.keepWeekly">
              启用每周备份保留
            </el-checkbox>
            <el-input-number v-model="ruleForm.keepWeeklyCount" :min="1" :disabled="!ruleForm.keepWeekly"
              style="width: 120px; margin-left: 10px"></el-input-number>
            <span class="count-label">个</span>
          </div>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            保留最近N周的备份
          </div>
        </el-form-item>

        <el-form-item label="每月备份">
          <div class="strategy-item">
            <el-checkbox v-model="ruleForm.keepMonthly">
              启用每月备份保留
            </el-checkbox>
            <el-input-number v-model="ruleForm.keepMonthlyCount" :min="1" :disabled="!ruleForm.keepMonthly"
              style="width: 120px; margin-left: 10px"></el-input-number>
            <span class="count-label">个</span>
          </div>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            保留最近N个月的备份
          </div>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveRule">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { backupService } from '@/services/backup.service'
import { backupRuleService } from '@/services/backup-rule.service'

export default {
  name: 'BackupRules',
  data() {
    return {
      rules: [],
      tasks: [],
      dialogVisible: false,
      editingRule: null,
      ruleForm: {
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
      }
    }
  },
  mounted() {
    this.loadRules()
    this.loadTasks()
  },
  methods: {
    async loadRules() {
      this.rules = await backupRuleService.getAllRules();
    },

    async loadTasks() {
      try {
        this.tasks = await backupService.getTasks()
      } catch (error) {
        this.$message.error('加载任务失败: ' + (error.response?.data || error.message))
      }
    },

    createRule() {
      this.editingRule = null
      this.ruleForm = {
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
      }
      this.dialogVisible = true
    },

    editRule(rule) {
      this.editingRule = rule
      this.ruleForm = { ...rule }
      this.dialogVisible = true
    },

    async saveRule() {
      try {
        await this.$refs.ruleForm.validate()

        let message = '';
        if (this.editingRule) {
          await backupRuleService.updateRule(this.editingRule.id, this.ruleForm);
          message = '规则更新成功';
        } else {
          await backupRuleService.createRule(this.ruleForm);
          message = '规则创建成功';
        }

        this.$message.success(message);
        this.dialogVisible = false;
        this.loadRules(); // 刷新列表
      } catch (error) {
        this.$message.error(error.response?.data?.message || error.message);
      }
    },

    async deleteRule(rule) {
      try {
        await this.$confirm('确认删除该规则吗？', '提示', {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        })

        await backupRuleService.deleteRule(rule.id)
        this.$message.success('删除成功')
        this.loadRules()
      } catch (error) {
        if (error !== 'cancel') {
          this.$message.error(error.response?.data?.message || error.message)
        }
      }
    }
  }
}
</script>

<style scoped>
.backup-rules-container {
  max-width: 90%;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  padding-bottom: 10px;
  border-bottom: 1px solid #eee;
}

.form-tip {
  margin-top: 5px;
  font-size: 12px;
  color: #909399;
}

.form-tip i {
  margin-right: 4px;
}

.strategy-item {
  display: flex;
  align-items: center;
  height: 40px;
}

.count-label {
  margin-left: 5px;
  color: #606266;
}
</style>