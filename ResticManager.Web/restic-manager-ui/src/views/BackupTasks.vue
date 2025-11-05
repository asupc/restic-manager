<template>
  <div class="backup-tasks-container">
    <div class="page-header">
      <h1>备份任务管理</h1>
      <div>
        <el-button type="primary" @click="openCreateDialog" icon="el-icon-plus" size="small">
          新增任务
        </el-button>
      </div>
    </div>

    <el-card class="filter-bar">
      <el-row :gutter="10">
        <el-col :span="6">
          <el-input v-model="filters.name" placeholder="搜索任务名称..." clearable @input="handleFilterChange"
            size="small"></el-input>
        </el-col>
        <el-col :span="6">
          <el-select v-model="filters.repositoryId" placeholder="选择仓库" clearable @change="handleFilterChange"
            size="small">
            <el-option label="全部仓库" :value="0"></el-option>
            <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id"></el-option>
          </el-select>
        </el-col>
        <el-col :span="6">
          <el-select v-model="filters.status" placeholder="选择状态" clearable @change="handleFilterChange" size="small">
            <el-option label="全部状态" value=""></el-option>
            <el-option label="启用" value="enabled"></el-option>
            <el-option label="禁用" value="disabled"></el-option>
          </el-select>
        </el-col>
      </el-row>
    </el-card>

    <el-card>
      <el-table :data="filteredTasks" style="width: 100%" v-if="filteredTasks?.length > 0">
        <el-table-column prop="name" label="任务名称">
          <template #default="scope">
            <span>{{ scope.row.name }}
            </span>
            <div style="font-size: 12px; color: #999;">
              <el-tag :type="scope.row.isEnabled ? 'success' : 'info'" size="mini" style="margin-right: 5px;">
                {{ scope.row.isEnabled ? '启用' : '禁用' }}
              </el-tag>
              <el-tag :type="getStatusTagType(scope.row.lastRunStatus)" size="mini">
                {{ getStatusText(scope.row.lastRunStatus) }}
              </el-tag>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="源路径">
          <template #default="scope">
            <span :title="scope.row.sourcePath">{{ truncatePath(scope.row.sourcePath) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="仓库">
          <template #default="scope">
            {{ getRepositoryName(scope.row.repositoryId) }}
          </template>
        </el-table-column>
        <el-table-column label="备份规则">
          <template #default="scope">
            {{ getBackupRuleName(scope.row.backupRuleId) || '未设置' }}
          </template>
        </el-table-column>
        <el-table-column label="计划" width="120">
          <template #default="scope">
            {{ scope.row.cronExpression || '无' }}
          </template>
        </el-table-column>
        <el-table-column label="上次运行" width="150">
          <template #default="scope">
            {{ scope.row.lastRunAt ? formatDate(scope.row.lastRunAt) : '从未' }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="350" fixed="right">
          <template #default="scope">
            <el-button title="编辑任务" size="mini" type="primary" @click="openEditDialog(scope.row)">编辑</el-button>
            <el-button title="执行任务" size="mini" type="primary" @click="runTask(scope.row)" :disabled="scope.row.running"
              :loading="scope.row.running">运行</el-button>
            <el-button title="查看记录" size="mini" type="primary" @click="viewRecords(scope.row)">记录</el-button>
            <el-button title="浏览文件" size="mini" type="primary" @click="browseFiles(scope.row)">浏览</el-button>
            <el-button title="删除任务" size="mini" type="danger" @click="deleteTask(scope.row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-empty v-if="!filteredTasks || filteredTasks.length === 0" description="暂无备份任务" class="empty-state">
        <el-button type="primary" @click="openCreateDialog" size="small">创建第一个任务</el-button>
      </el-empty>
    </el-card>

    <!-- 任务编辑对话框 -->
    <el-dialog :title="editingTask ? '编辑备份任务' : '新增备份任务'" :visible.sync="dialogVisible" width="600px"
      :close-on-click-modal="false" append-to-body>
      <el-form :model="form" :rules="rules" ref="formRef" label-position="left" label-width="120px">
        <el-form-item label="任务名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入任务名称" size="small"></el-input>
        </el-form-item>

        <el-form-item label="源路径" prop="sourcePath">
          <folder-selector v-model="form.sourcePath" title="选择源路径" placeholder="请选择或输入源路径">
          </folder-selector>
        </el-form-item>

        <el-form-item label="仓库" prop="repositoryId">
          <el-select v-model="form.repositoryId" placeholder="请选择仓库" class="w-100" size="small">
            <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id"></el-option>
          </el-select>
        </el-form-item>

        <el-form-item label="备份规则" prop="backupRuleId">
          <el-select v-model="form.backupRuleId" placeholder="请选择备份规则" clearable class="w-100" size="small">
            <el-option v-for="rule in backupRules" :key="rule.id" :label="rule.name" :value="rule.id"></el-option>
          </el-select>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            选择应用于该任务的备份规则（可选）
          </div>
        </el-form-item>

        <el-form-item label="计划 (Cron)" prop="cronExpression">
          <el-input v-model="form.cronExpression" placeholder="例如: 0 0 * * * (每天凌晨执行)" size="small"></el-input>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            使用Cron表达式设置备份计划，留空表示手动执行 例如: 0 0 * * * (每天凌晨执行)
          </div>
        </el-form-item>

        <el-form-item label="启用" prop="isEnabled">
          <el-switch v-model="form.isEnabled"></el-switch>
        </el-form-item>
      </el-form>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="dialogVisible = false" size="small">取消</el-button>
          <el-button type="primary" @click="saveTask" size="small">保存</el-button>
        </span>
      </template>
    </el-dialog>

    <!-- 备份记录抽屉 -->
    <el-drawer :title="`${currentTaskName} 的备份记录`" :visible.sync="drawerVisible" direction="rtl" size="600px">
      <div class="drawer-content">
        <div v-for="record in records" :key="record.id" class="record-item"
          :class="{ 'failed-record': record.status === 'Failed' }">
          <div class="record-header">
            <span :class="getStatusClass(record.status)">
              {{ getStatusText(record.status) }}
            </span>
            <span class="record-time">{{ formatDate(record.startTime) }}</span>
          </div>

          <div class="record-details">
            <div class="detail-grid">
              <div class="detail-item">
                <span class="detail-label">耗时:</span>
                <span>{{ calculateDuration(record.startTime, record.endTime) }}</span>
              </div>

              <div class="detail-item">
                <span class="detail-label">处理文件:</span>
                <span>{{ record.filesProcessed }}</span>
              </div>

              <div class="detail-item">
                <span class="detail-label">传输数据:</span>
                <span>{{ formatBytes(record.dataTransferred) }}</span>
              </div>

              <div v-if="record.summary && record.summary.files_new > 0" class="detail-item">
                <span class="detail-label">新增:</span>
                <span>{{ record.summary.files_new }}</span>
              </div>

              <div v-if="record.summary && record.summary.files_changed > 0" class="detail-item">
                <span class="detail-label">更改:</span>
                <span>{{ record.summary.files_changed }}</span>
              </div>

              <div v-if="record.summary && record.summary.files_unmodified > 0" class="detail-item">
                <span class="detail-label">未更改:</span>
                <span>{{ record.summary.files_unmodified }}</span>
              </div>
            </div>

            <div v-if="record.error" class="error-section">
              <div class="detail-label">错误信息:</div>
              <div class="error-content">{{ record.error }}</div>
            </div>

            <div class="record-actions">
              <el-button size="mini" type="text" @click="showLog(record)" v-if="record.output">
                查看日志
              </el-button>
            </div>
          </div>
        </div>
        <el-empty v-if="records.length === 0" description="暂无备份记录"></el-empty>
      </div>
    </el-drawer>

    <!-- 文件浏览抽屉 -->
    <el-drawer :title="`${currentTaskName} 的文件结构`" :visible.sync="fileDrawerVisible" direction="rtl" size="800px">
      <div class="drawer-content">
        <div class="file-browser-container">
          <el-alert v-if="currentExcludePatterns" :title="'当前排除规则: ' + currentExcludePatterns" type="info" show-icon
            size="small" style="margin-bottom: 10px;">
          </el-alert>

          <el-tree ref="fileTree" :data="fileTreeData" :props="fileTreeProps" :load="loadTreeNode" lazy node-key="path"
            :expand-on-click-node="false" :highlight-current="true">
            <span class="custom-tree-node" slot-scope="{ node, data }">
              <span :class="{ 'excluded-file': data.isExcluded }">
                <!-- <span class="excluded-file"> -->
                <i :class="data.icon"></i>
                {{ node.label }}
              </span>
            </span>
          </el-tree>

          <el-empty v-if="fileTreeData.length === 0" description="该目录下没有文件"></el-empty>
        </div>
      </div>
    </el-drawer>

    <!-- 日志弹窗 -->
    <el-dialog title="备份日志" :visible.sync="logDialogVisible" width="600px">
      <pre class="log-content">{{ currentLog }}</pre>
      <template #footer>
        <el-button @click="logDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui'
import { backupService } from '@/services/backup.service'
import { backupRecordService } from '@/services/backup-record.service'
import { backupRuleService } from '../services/backup-rule.service'
import { repositoryService } from '../services/repository.service'
import FolderSelector from '@/components/FolderSelector.vue'

export default {
  name: 'BackupTasks',
  components: {
    FolderSelector,
  },
  data() {
    return {
      tasks: [],
      repositories: [],
      backupRules: [],
      dialogVisible: false,
      drawerVisible: false,
      logDialogVisible: false,
      editingTask: null,
      showRuleForm: false,
      showScheduleForm: false,
      currentTaskId: null,
      currentTaskName: '',
      records: [],
      currentLog: '',
      form: {
        name: '',
        sourcePath: '',
        repositoryId: null,
        backupRuleId: null,
        cronExpression: '',
        isEnabled: true
      },
      scheduleForm: {
        taskId: null,
        cronExpression: ''
      },
      filters: {
        name: '',
        repositoryId: 0,
        status: ''
      },
      rules: {
        name: [{ required: true, message: '请输入任务名称', trigger: 'blur' }],
        sourcePath: [{ required: true, message: '请选择源路径', trigger: 'blur' }],
        repositoryId: [{ required: true, message: '请选择仓库', trigger: 'change' }]
      },
      fileDrawerVisible: false,
      fileTreeData: [],
      fileTreeProps: {
        label: 'name',
        children: 'children',
        isLeaf: 'isLeaf'
      },
      currentExcludePatterns: null,
      currentSourcePath: '',
    }
  },
  computed: {
    filteredTasks() {
      let result = this.tasks

      // 名称筛选
      if (this.filters.name) {
        result = result.filter(t => t.name.toLowerCase().includes(this.filters.name.toLowerCase()))
      }

      // 仓库筛选
      if (this.filters.repositoryId) {
        result = result.filter(t => t.repositoryId === this.filters.repositoryId)
      }

      // 状态筛选
      if (this.filters.status) {
        const isEnabled = this.filters.status === 'enabled'
        result = result.filter(t => t.isEnabled === isEnabled)
      }

      return result
    }
  },
  methods: {
    // 获取任务列表
    fetchTasks: async function () {
      this.tasks = await backupService.getTasks()
    },

    // 获取仓库列表
    fetchRepositories: async function () {
      this.repositories = await repositoryService.getRepositories()
    },

    // 获取备份规则列表
    fetchBackupRules: async function () {
      this.backupRules = await backupRuleService.getAllRules()
    },

    // 打开创建对话框
    openCreateDialog: function () {
      this.editingTask = null
      this.form = {
        name: '',
        sourcePath: '',
        repositoryId: null,
        backupRuleId: null,
        cronExpression: '',
        isEnabled: true
      }
      this.dialogVisible = true
    },

    // 打开编辑对话框
    openEditDialog: function (task) {
      this.editingTask = task
      this.form = { ...task }
      this.dialogVisible = true
    },

    // 保存任务
    saveTask: async function () {
      const valid = await this.$refs.formRef.validate()
      if (!valid) return

      const taskData = { ...this.form }

      if (this.editingTask) {
        await backupService.updateTask(this.editingTask.id, taskData)
        Message.success('任务更新成功！')
      } else {
        await backupService.createTask(taskData)
        Message.success('任务创建成功！')
      }

      this.dialogVisible = false
      this.fetchTasks() // 刷新列表
    },

    // 删除任务
    deleteTask: async function (task) {
      await MessageBox.confirm(
        `确定要删除备份任务 "${task.name}" 吗？`,
        '提示',
        {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )

      await backupService.deleteTask(task.id)
      Message.success('任务删除成功！')
      this.fetchTasks() // 刷新列表
    },

    // 切换任务启用状态
    toggleTaskStatus: async function (task) {
      // 更新任务状态
      const updatedTask = { ...task, isEnabled: !task.isEnabled }
      await backupService.updateTask(task.id, updatedTask)

      // 更新本地状态
      this.$set(task, 'isEnabled', !task.isEnabled)
      Message.success('任务状态更新成功！')
      this.fetchTasks() // 刷新列表
    },

    // 运行任务
    runTask: async function (task) {
      await backupService.runTask(task.id)
      Message.success('任务开始执行')
      this.fetchTasks() // 通过刷新获取最新的运行状态
    },

    // 查看备份记录
    viewRecords: async function (task) {
      this.currentTaskId = task.id
      this.currentTaskName = task.name
      this.records = await backupRecordService.getRecordsByTaskId(task.id)
      this.drawerVisible = true
    },

    // 显示日志
    showLog: function (record) {
      this.currentLog = record.output
      this.logDialogVisible = true
    },

    // 计算持续时间
    calculateDuration(startTime, endTime) {
      if (!startTime || !endTime) return 'N/A'

      const start = new Date(startTime)
      const end = new Date(endTime)
      const durationMs = end - start
      const seconds = Math.floor(durationMs / 1000)
      const minutes = Math.floor(seconds / 60)
      const hours = Math.floor(minutes / 60)

      if (hours > 0) {
        return `${hours}小时${minutes % 60}分钟${seconds % 60}秒`
      } else if (minutes > 0) {
        return `${minutes}分钟${seconds % 60}秒`
      } else {
        return `${seconds}秒`
      }
    },

    // 格式化字节大小
    formatBytes(bytes) {
      if (bytes === 0) return '0 Bytes'
      const k = 1024
      const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
      const i = Math.floor(Math.log(bytes) / Math.log(k))
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
    },

    // 获取状态CSS类
    getStatusClass(status) {
      switch (status) {
        case 'Succeeded':
        case 'success':
          return 'status-success'
        case 'Failed':
          return 'status-failed'
        case 'Running':
          return 'status-running'
        default:
          return 'status-default'
      }
    },

    // 处理筛选条件变化
    handleFilterChange() {
      // 筛选逻辑在computed中处理
    },

    // 工具函数：获取仓库名称
    getRepositoryName(repositoryId) {
      const repo = this.repositories.find(r => r.id === repositoryId)
      return repo ? repo.name : '未知仓库'
    },

    // 工具函数：获取备份规则名称
    getBackupRuleName(backupRuleId) {
      if (!backupRuleId) return ''
      const rule = this.backupRules.find(r => r.id === backupRuleId)
      return rule ? rule.name : '未知规则'
    },

    // 工具函数：格式化日期
    formatDate(dateString) {
      if (!dateString) return ''
      try {
        return new Date(dateString).toLocaleString()
      } catch (err) {
        console.error('无效日期格式:', dateString)
        return '无效日期'
      }
    },

    // 工具函数：获取状态标签类型
    getStatusTagType(status) {
      switch (status) {
        case 'Success':
        case 'Succeeded':
          return 'success'
        case 'Failed':
          return 'danger'
        case 'Running':
          return 'warning'
        default:
          return 'info'
      }
    },

    // 工具函数：获取状态文本
    getStatusText(status) {
      switch (status) {
        case 'Success':
        case 'Succeeded':
          return '成功'
        case 'Failed':
          return '失败'
        case 'Running':
          return '运行中'
        default:
          return '未运行'
      }
    },

    // 工具函数：截断长路径
    truncatePath(path) {
      if (!path) return ''
      if (path.length <= 50) return path
      return path.substring(0, 25) + '...' + path.substring(path.length - 22)
    },

    // 浏览文件
    browseFiles: async function (task) {
      this.currentTaskId = task.id;
      this.currentTaskName = task.name;
      this.currentSourcePath = task.sourcePath;
      this.fileDrawerVisible = true;
      this.currentExcludePatterns = "";
      this.fileTreeData = [];

      // 获取备份规则排除模式
      if (task.backupRuleId) {
        const rule = await backupRuleService.getRuleById(task.backupRuleId);
        this.currentExcludePatterns = rule.excludePatterns;
      }
      await this.loadRootFiles();
    },

    // 加载根目录文件
    loadRootFiles: async function () {
      try {
        const response = await backupService.getFolders(this.currentSourcePath);
        if (Array.isArray(response)) {
          this.fileTreeData = response.map(item => this.createTreeNode(item, this.currentSourcePath));
        } else {
          this.fileTreeData = [];
        }
      } catch (error) {
        this.fileTreeData = [];
        throw error;
      }
    },

    // 创建树节点
    createTreeNode: function (item, parentPath) {
      let fullPath;
      if (!parentPath) {
        fullPath = item.name;
      } else {
        // 检查parentPath是否以路径分隔符结尾
        if (parentPath.match(/[/\\]$/)) {
          fullPath = parentPath + item.name;
        } else {
          // 根据parentPath中已有的分隔符决定使用哪种分隔符
          const separator = parentPath.includes('\\') ? '\\' : '/';
          fullPath = parentPath + separator + item.name;
        }
      }

      // 判断是否被排除
      const isExcluded = this.isFileExcluded(fullPath);

      // 设置图标
      let icon = 'el-icon-document';
      if (item.type === 'folder') {
        icon = 'el-icon-folder';
      } else if (item.type === 'drive') {
        icon = 'el-icon-box';
      }

      return {
        name: item.name,
        path: fullPath,
        type: item.type,
        icon: icon,
        isLeaf: item.type === 'file',
        isExcluded: isExcluded
      };
    },

    // 加载树节点
    loadTreeNode: async function (node, resolve) {
      if (node.level === 0) {
        return resolve(this.fileTreeData);
      }

      const path = node.data.path;
      try {
        const response = await backupService.getFolders(path);
        const nodes = response.map(item => this.createTreeNode(item, path));
        resolve(nodes);
      } catch (error) {
        console.error('加载子目录失败:', error);
        Message.error('加载子目录失败: ' + (error.response?.data?.message || error.message));
        resolve([]);
      }
    },

    // 判断文件是否被排除
    isFileExcluded: function (filePath) {
      if (!this.currentExcludePatterns) {
        return false;
      }

      try {
        // 将Windows路径转换为Unix风格路径进行匹配
        const normalizedPath = filePath.replace(/\\/g, '/');

        return this.currentExcludePatterns.split(";").some(pattern => {
          if (!pattern) return false;

          // 处理 ** 通配符模式 (例如 **/node_modules/)
          if (pattern.startsWith('**/')) {
            const suffix = pattern.substring(3); // 获取 **/ 后面的部分
            // 检查路径是否以指定后缀结尾，或者包含该后缀作为目录
            return normalizedPath.endsWith('/' + suffix) ||
              normalizedPath.includes('/' + suffix);
          }

          // 处理 * 通配符模式 (例如 *.log)
          if (pattern.startsWith('*.')) {
            const extension = pattern.substring(1); // 获取 .* 后面的部分 (包括点)
            return normalizedPath.endsWith(extension);
          }

          // 如果模式以 / 结尾，表示目录匹配
          if (pattern.endsWith('/')) {
            return normalizedPath.startsWith(pattern) ||
              normalizedPath.includes('/' + pattern) ||
              normalizedPath === pattern.slice(0, -1);
          }
          // 文件或目录名匹配
          return normalizedPath.includes('/' + pattern) ||
            normalizedPath.endsWith(pattern) ||
            normalizedPath === pattern;
        });
      } catch (e) {
        console.error('文件排除检查出错:', e);
        return false;
      }
    },
  },
  mounted() {
    this.fetchTasks()
    this.fetchRepositories()
    this.fetchBackupRules()
  }
}
</script>

<style scoped>
.backup-tasks-container {
  max-width: 90%;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 15px;
  padding-bottom: 8px;
  border-bottom: 1px solid #eee;
}

.filter-bar {
  margin-bottom: 15px;
  padding: 10px !important;
}

.loading-state {
  text-align: center;
  padding: 30px 0;
}

.loading-state p {
  margin-top: 10px;
  color: #666;
}

.empty-state {
  padding: 40px 0;
}

.form-tip {
  margin-top: 5px;
  font-size: 12px;
  color: #909399;
}

.form-tip i {
  margin-right: 4px;
}

.w-100 {
  width: 100%;
}

.drawer-content {
  padding: 10px;
  height: 100%;
  overflow-y: auto;
}

.record-item {
  border: 1px solid #ebeef5;
  border-radius: 4px;
  margin-bottom: 8px;
  font-size: 12px;
  background-color: #fff;
}

.record-item.failed-record {
  border-color: #f56c6c;
}

.record-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 10px;
  border-bottom: 1px solid #ebeef5;
  background-color: #f5f7fa;
}

.record-time {
  font-size: 11px;
  color: #909399;
}

.status-success {
  color: #67c23a;
  font-weight: bold;
}

.status-failed {
  color: #f56c6c;
  font-weight: bold;
}

.status-running {
  color: #e6a23c;
  font-weight: bold;
}

.status-default {
  color: #909399;
  font-weight: bold;
}

.record-details {
  padding: 8px 10px;
}

.detail-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.detail-item {
  display: flex;
  justify-content: space-between;
  line-height: 1.2;
  padding: 3px;
}

.detail-label {
  font-weight: bold;
  color: #606266;
  margin-right: 5px;
}

.error-section {
  margin-top: 6px;
  padding: 6px;
  background-color: #fef0f0;
  border-radius: 3px;
}

.error-content {
  margin-top: 3px;
  color: #f56c6c;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 11px;
}

.record-actions {
  margin-top: 6px;
  text-align: right;
}

.log-content {
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 500px;
  overflow-y: auto;
}

.file-browser-container {
  padding: 10px;
  height: 100%;
  overflow-y: auto;
}

.custom-tree-node {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 14px;
  padding-right: 8px;
}

.excluded-file {
  color: #999;
  text-decoration: line-through;
}

.el-drawer__title>.el-drawer__header {
  margin-bottom: 10px !important
}
</style>