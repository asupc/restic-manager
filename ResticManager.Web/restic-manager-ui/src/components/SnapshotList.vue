<template>
  <el-card>
    <el-table :data="snapshots" style="width: 100%" v-if="snapshots.length > 0">
      <el-table-column type="expand" v-if="showDetails">
        <template slot-scope="props">
          <el-card shadow="never" class="snapshot-summary-card">
            <div slot="header" class="clearfix">
              <span>快照摘要信息</span>
            </div>
            <el-row :gutter="20">
              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">未修改文件:</span>
                  <span class="summary-value">{{ props.row.summary.files_unmodified }}</span>
                </div>
              </el-col>

              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">新增目录:</span>
                  <span class="summary-value">{{ props.row.summary.dirs_new }}</span>
                </div>
              </el-col>
              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">修改目录:</span>
                  <span class="summary-value">{{ props.row.summary.dirs_changed }}</span>
                </div>
              </el-col>
              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">未修改目录:</span>
                  <span class="summary-value">{{ props.row.summary.dirs_unmodified }}</span>
                </div>
              </el-col>

              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">新增数据:</span>
                  <span class="summary-value">{{ formatFileSize(props.row.summary.data_added) }}</span>
                </div>
              </el-col>
              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">总处理文件:</span>
                  <span class="summary-value">{{ props.row.summary.total_files_processed }}</span>
                </div>
              </el-col>
              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">总处理大小:</span>
                  <span class="summary-value">{{ formatFileSize(props.row.summary.total_bytes_processed) }}</span>
                </div>
              </el-col>

              <el-col :span="6">
                <div class="summary-item">
                  <span class="summary-label">数据块:</span>
                  <span class="summary-value">{{ props.row.summary.data_blobs }}</span>
                </div>
              </el-col>
            </el-row>
          </el-card>
        </template>
      </el-table-column>

      <el-table-column prop="hostname" label="主机名" width="150"></el-table-column>
      <el-table-column prop="time" label="开始时间" width="200">
        <template slot-scope="scope">
          {{ new Date(scope.row.summary.backup_start).toLocaleString() }}
        </template>
      </el-table-column>
      <el-table-column label="总用时" width="120">
        <template slot-scope="scope">
          <span>{{ formatDuration(scope.row.summary.backup_end, scope.row.summary.backup_start) }}</span>
        </template>
      </el-table-column>
      <el-table-column label="总大小" width="120">
        <template slot-scope="scope">
          <span class="size-total">{{ formatFileSize(scope.row.summary.total_bytes_processed) }}</span>
        </template>
      </el-table-column>
      <el-table-column label="总文件" width="100">
        <template slot-scope="scope">
          <span class="files-total">{{ scope.row.summary.total_files_processed }}</span>
        </template>
      </el-table-column>
      <el-table-column label="新增文件" width="100">
        <template slot-scope="scope">
          <span class="files-new">{{ scope.row.summary.files_new }}</span>
        </template>
      </el-table-column>
      <el-table-column label="修改文件" width="100">
        <template slot-scope="scope">
          <span class="files-changed">{{ scope.row.summary.files_changed }}</span>
        </template>
      </el-table-column>
      <el-table-column label="路径">
        <template slot-scope="scope">
          <div v-for="path in scope.row.paths" :key="path">{{ path }}</div>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="250">
        <template slot-scope="scope">
          <el-button size="mini" @click="onViewFiles(scope.row)">查看文件</el-button>
          <el-button size="mini" type="primary" @click="onRestore(scope.row)">恢复</el-button>
          <el-button size="mini" type="danger" @click="onDelete(scope.row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    <el-empty v-else description="暂无快照"></el-empty>

    <!-- 恢复快照对话框 -->
    <el-dialog :visible.sync="restoreDialogVisible" title="恢复快照" width="500px" :close-on-click-modal="false"
      append-to-body>
      <el-form :model="restoreForm" label-position="left" label-width="120px">
        <el-form-item label="恢复路径">
          <folder-selector v-model="restoreForm.path" title="选择恢复路径" placeholder="请选择或输入恢复路径">
          </folder-selector>
          <div class="form-tip">
            <i class="el-icon-info"></i>
            选择文件恢复的目标路径
          </div>
        </el-form-item>
      </el-form>
      <span slot="footer" class="dialog-footer">
        <el-button @click="restoreDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="performRestore">恢复</el-button>
      </span>
    </el-dialog>

    <!-- 删除快照对话框 -->
    <el-dialog :visible.sync="deleteDialogVisible" title="删除快照" width="500px" :close-on-click-modal="false"
      append-to-body>
      <el-alert title="警告" type="warning" description="您确定要删除这个快照吗？此操作无法撤销。" show-icon :closable="false">
      </el-alert>
      <div style="margin-top: 20px;">
        <p><strong>快照ID:</strong> {{ deleteItem?.short_id }}</p>
        <p><strong>主机名:</strong> {{ deleteItem?.hostname }}</p>
        <p><strong>时间:</strong> {{ deleteItem ? new Date(deleteItem.summary.backup_start).toLocaleString() : ''
          }}</p>
      </div>
      <span slot="footer" class="dialog-footer">
        <el-button @click="deleteDialogVisible = false">取消</el-button>
        <el-button type="danger" @click="performDelete">删除</el-button>
      </span>
    </el-dialog>

  </el-card>
</template>

<script>
import { Message, MessageBox } from 'element-ui'
import FolderSelector from '@/components/FolderSelector.vue'

export default {
  name: 'SnapshotList',
  components: {
    FolderSelector
  },
  props: {
    snapshots: {
      type: Array,
      default: () => []
    },
    showDetails: {
      type: Boolean,
      default: true
    },
    repositoryId: {
      type: [String, Number],
      required: true
    }
  },
  data() {
    return {
      restoreDialogVisible: false,
      deleteDialogVisible: false,
      restoreItem: null,
      deleteItem: null,
      restoreForm: {
        path: ''
      }
    }
  },
  methods: {
    onViewFiles(snapshot) {
      this.$router.push(`/repositories/${this.repositoryId}/snapshots/${snapshot.id}/files?shortId=${snapshot.short_id}&path=${snapshot.paths[0]}`)
    },
    onRestore(snapshot) {
      this.restoreItem = snapshot
      this.restoreForm.path = ''
      this.restoreDialogVisible = true
    },
    onDelete(snapshot) {
      this.deleteItem = snapshot
      this.deleteDialogVisible = true
    },
    performRestore() {
      // 子类需要实现具体的恢复逻辑
      this.$emit('restore-snapshot', {
        repositoryId: this.repositoryId,
        snapshot: this.restoreItem,
        targetPath: this.restoreForm.path
      })
      this.restoreDialogVisible = false
    },
    performDelete() {
      // 子类需要实现具体的删除逻辑
      this.$emit('delete-snapshot', {
        repositoryId: this.repositoryId,
        snapshot: this.deleteItem
      })
      this.deleteDialogVisible = false
    },
    formatFileSize(bytes) {
      if (bytes === 0) return '0 Bytes'
      const k = 1024
      const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
      const i = Math.floor(Math.log(bytes) / Math.log(k))
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
    },
    formatDuration(endTime, startTime) {
      if (!endTime || !startTime) return 'N/A'

      const start = new Date(startTime)
      const end = new Date(endTime)
      const durationMs = end - start

      if (durationMs < 0) return '无效时间'

      const seconds = Math.floor(durationMs / 1000)
      const minutes = Math.floor(seconds / 60)
      const hours = Math.floor(minutes / 60)

      if (hours > 0) {
        return `${hours}h ${minutes % 60}m ${seconds % 60}s`
      } else if (minutes > 0) {
        return `${minutes}m ${seconds % 60}s`
      } else {
        return `${seconds}s`
      }
    }
  }
}
</script>

<style scoped>
.snapshot-summary-card {
  margin-left: 50px;
  margin-right: 50px;
  margin-top: 10px;
  margin-bottom: 10px;
}

.summary-item {
  display: flex;
  justify-content: space-between;
  padding: 5px 0;
}

.summary-label {
  font-weight: bold;
  color: #606266;
}

.summary-value {
  color: #909399;
}

.files-total {
  color: #409EFF;
  font-weight: bold;
}

.files-new {
  color: #67C23A;
  font-weight: bold;
}

.files-changed {
  color: #E6A23C;
  font-weight: bold;
}

.size-total {
  color: #909399;
  font-weight: bold;
}

.dialog-footer {
  text-align: right;
}

.form-tip {
  margin-top: 5px;
  font-size: 12px;
  color: #909399;
}

.form-tip i {
  margin-right: 4px;
}
</style>