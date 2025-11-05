<template>
  <div class="home-page">
    <div class="page-header">
      <h1>工作台</h1>
      <!-- <div>
        <el-button type="primary" @click="fetchDashboardData" icon="el-icon-refresh">
          刷新
        </el-button>
      </div> -->
    </div>

    <!-- 统计卡片 -->
    <div class="stats-container">
      <el-row :gutter="20">
        <el-col :span="6">
          <el-card class="stat-card primary-card">
            <div class="stat-content">
              <div class="stat-header">总任务数</div>
              <div class="stat-value">{{ totalTasks }}</div>
              <div class="stat-footer">
                <i class="el-icon-tickets stat-icon"></i>
                <span class="stat-desc">备份任务总数</span>
              </div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="stat-card warning-card">
            <div class="stat-content">
              <div class="stat-header">运行中任务</div>
              <div class="stat-value">{{ runningTasks }}</div>
              <div class="stat-footer">
                <i class="el-icon-loading stat-icon"></i>
                <span class="stat-desc">正在执行的任务</span>
              </div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="stat-card success-card">
            <div class="stat-content">
              <div class="stat-header">成功备份</div>
              <div class="stat-value">{{ successRecords }}</div>
              <div class="stat-footer">
                <i class="el-icon-circle-check stat-icon"></i>
                <span class="stat-desc">今日成功完成的备份</span>
              </div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="stat-card info-card">
            <div class="stat-content">
              <div class="stat-header">存储库数</div>
              <div class="stat-value">{{ totalRepositories }}</div>
              <div class="stat-footer">
                <i class="el-icon-folder stat-icon"></i>
                <span class="stat-desc">已配置的存储库数量</span>
              </div>
            </div>
          </el-card>
        </el-col>
      </el-row>
    </div>

    <!-- 图表和状态 -->
    <el-row :gutter="20" style="margin-top: 20px;">
      <!-- 备份趋势图 -->
      <el-col :span="16">
        <el-card class="chart-card" v-loading="!chartDataLoaded">
          <div slot="header" class="card-header">
            <span>备份趋势</span>
          </div>
          <div class="chart-container">
            <div class="chart-placeholder">
              <p>图表功能将在后续版本中实现</p>
            </div>
          </div>
        </el-card>
      </el-col>

      <!-- 状态概览 -->
      <el-col :span="8">
        <el-card class="status-card">
          <div slot="header" class="card-header">
            <span>状态概览</span>
          </div>
          <div class="status-container">
            <div class="status-item">
              <span>备份规则</span>
              <el-tag>{{ totalRules }}</el-tag>
            </div>
            <div class="status-item">
              <span>最近24小时备份</span>
              <el-tag type="success">{{ last24HoursRecords }}</el-tag>
            </div>
            <div class="status-item">
              <span>失败的备份</span>
              <el-tag type="danger">{{ failedRecords }}</el-tag>
            </div>
            <div class="status-item">
              <span>系统健康</span>
              <el-tag :type="systemHealthStatus.type">{{ systemHealthStatus.text }}</el-tag>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 最近任务和活动 -->
    <el-row :gutter="20" style="margin-top: 20px;">
      <el-col :span="16">
        <el-card class="recent-card">
          <div slot="header" class="card-header">
            <span>最近备份任务</span>
          </div>
          <el-table :data="recentTasks" border style="width: 100%">
            <el-table-column prop="taskName" label="任务名称" min-width="150">
              <template #default="scope">
                <span class="task-name">{{ scope.row.taskName }}</span>
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100" align="center">
              <template #default="scope">
                <el-tag :type="getStatusType(scope.row.status)">
                  {{ getStatusText(scope.row.status) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="startTime" label="开始时间" width="170" align="center">
              <template #default="scope">{{ formatDate(scope.row.startTime) }}</template>
            </el-table-column>
            <el-table-column prop="endTime" label="结束时间" width="170" align="center">
              <template #default="scope">{{ scope.row.endTime ? formatDate(scope.row.endTime) : '-' }}</template>
            </el-table-column>
            <el-table-column prop="duration" label="耗时" width="100" align="center"></el-table-column>
            <el-table-column label="操作" width="100" align="center">
              <template #default="scope">
                <el-button size="mini" type="primary" @click="viewTaskDetails(scope.row)">详情</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>

      <el-col :span="8">
        <el-card class="activity-card">
          <div slot="header" class="card-header">
            <span>系统活动</span>
          </div>
          <div class="activity-container">
            <div v-if="activities.length === 0" class="no-activity">
              <p>暂无活动</p>
            </div>
            <ul v-else class="activity-list">
              <li v-for="(activity, index) in activities" :key="index" class="activity-item">
                <i class="el-icon-time activity-icon"></i>
                <div class="activity-content">
                  <p class="activity-text">{{ activity.text }}</p>
                  <p class="activity-time">{{ activity.time }}</p>
                </div>
              </li>
            </ul>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script>
import { backupService } from '@/services/backup.service'
import { backupRecordService } from '@/services/backup-record.service'
import { repositoryService } from '@/services/repository.service'
import { backupRuleService } from '@/services/backup-rule.service'

export default {
  data() {
    return {
      // 统计数据
      totalTasks: 0,
      runningTasks: 0,
      successRecords: 0,
      totalFiles: 0,
      totalRepositories: 0,
      totalRules: 0,
      last24HoursRecords: 0,
      failedRecords: 0,

      // 系统健康状态
      systemHealthStatus: {
        type: 'success',
        text: '正常'
      },

      // 最近任务
      recentTasks: [],

      // 系统活动
      activities: [
        { text: '系统初始化完成', time: '刚刚' },
        { text: '数据库连接成功', time: '5分钟前' }
      ],

      // 图表数据加载状态
      chartDataLoaded: false
    }
  },
  mounted() {
    this.fetchDashboardData()
  },
  methods: {
    async fetchDashboardData() {
      try {
        // 获取所有数据
        const [tasks, records, repositories, rules] = await Promise.all([
          backupService.getTasks(),
          backupRecordService.getRecords(),
          repositoryService.getRepositories(),
          backupRuleService.getAllRules()
        ])

        // 处理统计数据
        this.totalTasks = tasks.length
        this.runningTasks = tasks.filter(t => t.isRunning).length

        // 成功记录（今天）
        const today = new Date()
        today.setHours(0, 0, 0, 0)
        this.successRecords = records.filter(r => {
          const recordDate = new Date(r.startTime)
          return r.status === 'Success' && recordDate >= today
        }).length

        this.totalRepositories = repositories.length
        this.totalRules = rules.length

        // 最近24小时记录
        const twentyFourHoursAgo = new Date(Date.now() - 24 * 60 * 60 * 1000)
        this.last24HoursRecords = records.filter(r => {
          const recordDate = new Date(r.startTime)
          return recordDate >= twentyFourHoursAgo
        }).length

        // 失败记录
        this.failedRecords = records.filter(r => r.status === 'Failed').length

        // 处理最近任务（取最近5个）
        this.recentTasks = records
          .sort((a, b) => new Date(b.startTime) - new Date(a.startTime))
          .slice(0, 5)
          .map(record => ({
            ...record,
            taskName: tasks.find(t => t.id === record.taskId)?.name || '未知任务',
            duration: this.calculateDuration(record.startTime, record.endTime)
          }))

        // 设置系统健康状态
        if (this.failedRecords > 0) {
          this.systemHealthStatus = { type: 'warning', text: '警告' }
        }
        if (this.failedRecords > 3) {
          this.systemHealthStatus = { type: 'danger', text: '异常' }
        }

        // 标记图表数据已加载
        this.chartDataLoaded = true
      } catch (err) {
        this.$message.error('获取仪表盘数据失败: ' + err.message)
        console.error(err)
      }
    },

    formatDate(dateString) {
      if (!dateString) return '-'
      return new Date(dateString).toLocaleString('zh-CN')
    },

    calculateDuration(start, end) {
      if (!start) return '-'
      if (!end) return '运行中'

      const startDate = new Date(start)
      const endDate = new Date(end)
      const diffMs = endDate - startDate

      const seconds = Math.floor(diffMs / 1000) % 60
      const minutes = Math.floor(diffMs / (1000 * 60)) % 60
      const hours = Math.floor(diffMs / (1000 * 60 * 60))

      if (hours > 0) {
        return `${hours}h ${minutes}m ${seconds}s`
      } else if (minutes > 0) {
        return `${minutes}m ${seconds}s`
      } else {
        return `${seconds}s`
      }
    },

    getStatusType(status) {
      const statusMap = {
        'Success': 'success',
        'Failed': 'danger',
        'Running': 'warning',
        'Pending': 'info'
      }
      return statusMap[status] || 'info'
    },

    getStatusText(status) {
      const statusMap = {
        'Success': '成功',
        'Failed': '失败',
        'Running': '运行中',
        'Pending': '待处理'
      }
      return statusMap[status] || status
    },

    viewTaskDetails(row) {
      this.$router.push({ name: 'BackupRecords', query: { taskId: row.taskId } })
    }
  }
}
</script>

<style scoped>
.home-page {
  min-height: 100vh;
  /* background-color: #f5f7fa; */
  max-width: 90%;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.page-header h1 {
  font-size: 24px;
  font-weight: 500;
  color: #303133;
  margin: 0;
}

.stats-container {
  margin-bottom: 20px;
}

/* 统计卡片样式 */
.stat-card {
  border-radius: 8px;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
  transition: all 0.3s ease;
  height: 140px;
}

.stat-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 4px 20px 0 rgba(0, 0, 0, 0.15);
}

.stat-content {
  flex-direction: column;
  height: 100%;
}

.stat-header {
  font-size: 16px;
  color: #606266;
  margin-bottom: 10px;
}

.stat-value {
  font-size: 28px;
  font-weight: bold;
  margin: 10px 0;
  flex-grow: 1;
  display: flex;
  align-items: center;
}

.stat-footer {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  margin-top: 10px;
}

.stat-icon {
  font-size: 20px;
  margin-right: 8px;
}

.stat-desc {
  font-size: 14px;
  color: #909399;
}

.primary-card {
  border-left: 4px solid #409eff;
}

.warning-card {
  border-left: 4px solid #e6a23c;
}

.success-card {
  border-left: 4px solid #67c23a;
}

.info-card {
  border-left: 4px solid #909399;
}

/* 卡片通用样式 */
.chart-card,
.status-card,
.recent-card,
.activity-card {
  border-radius: 8px;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
}

.card-header {
  font-weight: 500;
  color: #303133;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.chart-container {
  height: 300px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.chart-placeholder p {
  color: #909399;
  font-size: 16px;
}

.status-container {
  padding: 10px 0;
}

.status-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 0;
  border-bottom: 1px solid #ebeef5;
}

.status-item:last-child {
  border-bottom: none;
}

.activity-container {
  min-height: 300px;
}

.no-activity {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #909399;
}

.activity-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.activity-item {
  display: flex;
  padding: 15px 0;
  border-bottom: 1px solid #ebeef5;
}

.activity-item:last-child {
  border-bottom: none;
}

.activity-icon {
  font-size: 18px;
  color: #409eff;
  margin-right: 10px;
  margin-top: 2px;
}

.activity-text {
  margin: 0;
  font-size: 14px;
  color: #606266;
}

.activity-time {
  margin: 5px 0 0;
  font-size: 12px;
  color: #909399;
}

.task-name {
  font-weight: 500;
  color: #303133;
}
</style>