<template>
  <div class="backup-records">
    <el-container style="padding: 20px;">
      <el-page-header :content="`备份记录 - ${taskName || '未知任务'}`" @back="handleBack"></el-page-header>
      <el-row :gutter="20" style="margin-top: 20px; margin-bottom: 15px;">
        <el-col :span="6">
          <el-select v-model="statusFilter" placeholder="选择状态" @change="handleStatusFilter" clearable filterable>
            <el-option label="全部状态" value=""></el-option>
            <el-option label="运行中" value="Running"></el-option>
            <el-option label="成功" value="Succeeded"></el-option>
            <el-option label="失败" value="Failed"></el-option>
          </el-select>
        </el-col>
        <el-col :span="14">
          <el-date-picker v-model="dateRange" type="daterange" range-separator="至" start-placeholder="开始日期"
            end-placeholder="结束日期" @change="handleDateFilter" style="width: 100%;"></el-date-picker>
        </el-col>
        <el-col :span="4" style="text-align: right;">
          <el-button type="primary" @click="resetFilters">重置</el-button>
        </el-col>
      </el-row>

      <el-card shadow="never">
        <el-table :data="filteredRecords" border element-loading-text="加载记录中..." stripe>
          <el-table-column prop="id" label="ID" width="80" align="center"></el-table-column>
          <el-table-column label="状态" width="100" align="center">
            <template #default="scope">
              <el-tag :type="getTagType(scope.row.status)" size="small">
                {{ formatStatus(scope.row.status) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="startTime" label="开始时间" width="180">
            <template #default="scope">{{ formatDate(scope.row.startTime) }}</template>
          </el-table-column>
          <el-table-column prop="endTime" label="结束时间" width="180">
            <template #default="scope">
              {{ scope.row.endTime ? formatDate(scope.row.endTime) : '运行中' }}
            </template>
          </el-table-column>
          <el-table-column label="耗时" width="120">
            <template #default="scope">
              {{ calculateDuration(scope.row.startTime, scope.row.endTime) }}
            </template>
          </el-table-column>
          <el-table-column prop="filesProcessed" label="处理文件数" width="120" align="right"></el-table-column>
          <el-table-column prop="dataTransferred" label="传输数据" width="140" align="right">
            <template #default="scope">
              {{ scope.row.dataTransferred ? formatSize(scope.row.dataTransferred) : '-' }}
            </template>
          </el-table-column>
          <el-table-column label="操作" width="150" align="center">
            <template #default="scope">
              <el-button size="mini" type="text" @click="showLog(scope.row)" style="color: #409EFF;">
                查看日志
              </el-button>
              <el-button size="mini" type="text" @click="showDetails(scope.row)" style="color: #67C23A;"
                v-if="scope.row.summary">
                详细信息
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination v-if="filteredRecords.length > 0" @size-change="handleSizeChange"
          @current-change="handleCurrentChange" :current-page="currentPage" :page-sizes="[10, 20, 50]"
          :page-size="pageSize" layout="total, sizes, prev, pager, next, jumper" :total="filteredRecords.length"
          style="margin-top: 15px; text-align: right"></el-pagination>
      </el-card>
    </el-container>

    <!-- 日志弹窗 -->
    <el-dialog title="备份日志" :visible.sync="showLogDialog" width="80%" :close-on-click-modal="false" append-to-body
      max-height="80vh">
      <div style="height: 60vh;">
        <el-scrollbar style="height: 100%;">
          <pre class="log-content">{{ currentLog }}</pre>
        </el-scrollbar>
      </div>
      <template #footer>
        <el-button type="primary" @click="showLogDialog = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 详细信息弹窗 -->
    <el-dialog title="备份详细信息" :visible.sync="showDetailsDialog" width="60%" :close-on-click-modal="false"
      append-to-body>
      <el-form label-position="left" label-width="150px" v-if="currentRecord && currentRecord.summary">
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="快照ID:">
              <span>{{ currentRecord.summary.snapshotId }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="总耗时:">
              <span>{{ currentRecord.summary.totalDuration.toFixed(2) }} 秒</span>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="新增文件数:">
              <span>{{ currentRecord.summary.filesNew }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="修改文件数:">
              <span>{{ currentRecord.summary.filesChanged }}</span>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="未修改文件数:">
              <span>{{ currentRecord.summary.filesUnmodified }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="处理文件总数:">
              <span>{{ currentRecord.summary.totalFilesProcessed }}</span>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="新增目录数:">
              <span>{{ currentRecord.summary.dirsNew }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="修改目录数:">
              <span>{{ currentRecord.summary.dirsChanged }}</span>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="未修改目录数:">
              <span>{{ currentRecord.summary.dirsUnmodified }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="处理数据总量:">
              <span>{{ formatSize(currentRecord.summary.totalBytesProcessed) }}</span>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="新增数据:">
              <span>{{ formatSize(currentRecord.summary.dataAdded) }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="打包后新增数据:">
              <span>{{ formatSize(currentRecord.summary.dataAddedPacked) }}</span>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="数据块数:">
              <span>{{ currentRecord.summary.dataBlobs }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="树块数:">
              <span>{{ currentRecord.summary.treeBlobs }}</span>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="备份开始时间:">
              <span>{{ formatDate(currentRecord.summary.backupStart) }}</span>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="备份结束时间:">
              <span>{{ formatDate(currentRecord.summary.backupEnd) }}</span>
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>

      <template #footer>
        <el-button type="primary" @click="showDetailsDialog = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { backupRecordService } from '@/services/backup-record.service'

export default {
  props: {
    taskId: {
      type: Number,
      required: true
    },
    taskName: {
      type: String,
      default: ''
    }
  },
  data() {
    return {
      // 数据
      allRecords: [],
      filteredRecords: [],
      // 筛选条件
      statusFilter: '',
      dateRange: [],
      // 分页
      currentPage: 1,
      pageSize: 10,
      // 日志弹窗
      showLogDialog: false,
      currentLog: '',
      // 详细信息弹窗
      showDetailsDialog: false,
      currentRecord: null
    }
  },
  mounted() {
    this.loadRecords()
  },
  methods: {
    // 返回上一页
    handleBack() {
      this.$router.go(-1)
    },

    // 加载备份记录
    async loadRecords() {
      this.allRecords = await backupRecordService.getRecordsByTaskId(this.taskId)
      this.applyFilters()
    },

    // 状态筛选
    handleStatusFilter() {
      this.applyFilters()
    },

    // 日期筛选
    handleDateFilter() {
      this.applyFilters()
    },

    // 重置筛选条件
    resetFilters() {
      this.statusFilter = ''
      this.dateRange = []
      this.applyFilters()
    },

    // 应用所有筛选条件
    applyFilters() {
      let result = [...this.allRecords]

      // 状态筛选
      if (this.statusFilter) {
        result = result.filter(r => r.status === this.statusFilter)
      }

      // 日期筛选
      if (this.dateRange && this.dateRange.length === 2) {
        const start = new Date(this.dateRange[0])
        const end = new Date(this.dateRange[1])
        end.setHours(23, 59, 59, 999) // 结束日期包含当天

        result = result.filter(r => {
          const recordDate = new Date(r.startTime)
          return recordDate >= start && recordDate <= end
        })
      }

      this.filteredRecords = result
      this.currentPage = 1
    },

    // 分页处理
    handleSizeChange(val) {
      this.pageSize = val
      this.currentPage = 1
    },
    handleCurrentChange(val) {
      this.currentPage = val
    },

    // 查看日志
    showLog(record) {
      this.currentLog = record.output || '无日志信息'
      this.showLogDialog = true
    },

    // 查看详细信息
    showDetails(record) {
      this.currentRecord = record
      this.showDetailsDialog = true
    },

    // 工具函数
    formatDate(dateString) {
      return new Date(dateString).toLocaleString()
    },
    formatStatus(status) {
      const map = {
        'Running': '运行中',
        'Succeeded': '成功',
        'Failed': '失败'
      }
      return map[status] || status
    },
    getTagType(status) {
      const map = {
        'Running': 'warning',
        'Succeeded': 'success',
        'Failed': 'danger'
      }
      return map[status] || 'info'
    },
    calculateDuration(start, end) {
      if (!end) return '运行中'
      const startDate = new Date(start)
      const endDate = new Date(end)
      const seconds = Math.floor((endDate - startDate) / 1000)
      return `${seconds}s`
    },
    formatSize(bytes) {
      if (bytes === 0) return '0 B'
      if (bytes < 1024) return `${bytes} B`
      if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`
      if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(2)} MB`
      return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`
    }
  }
}
</script>

<style scoped>
.backup-records {
  min-height: 100vh;
  background-color: #f5f7fa;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
}

.el-table .cell {
  padding: 8px 10px;
}

.log-content {
  white-space: pre-wrap;
  word-wrap: break-word;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #2c3e50;
  padding: 15px;
  background-color: #f8f9fa;
  border-radius: 4px;
  border: 1px solid #e9ecef;
  margin: 0;
}
</style>