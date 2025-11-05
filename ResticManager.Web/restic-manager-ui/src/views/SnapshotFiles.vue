<template>
  <div class="snapshot-files-container">
    <div class="page-header">
      <el-page-header @back="goBack" :content="`仓库${repository?.name}快照 ${snapshotShortId} 的文件`">
      </el-page-header>
    </div>

    <el-card>
      <el-table size="mini" :data="files" style="width: 100%" v-if="files.length > 0" row-key="path"
        :tree-props="{ children: 'children', hasChildren: 'hasChildren' }" lazy :load="loadSubdirectory">
        <el-table-column prop="name" label="名称" width="450">
          <template slot-scope="scope">
            <div class="name-wrapper">
              <i :class="scope.row.type === 'dir' ? 'el-icon-folder' : 'el-icon-document'"></i>
              <span :title="scope.row.name" class="name-text">{{ scope.row.name }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="size" label="大小" width="150">
          <template slot-scope="scope">
            <span v-if="scope.row.type === 'file'">{{ formatFileSize(scope.row.size) }}</span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column prop="path" label="路径">
          <template slot-scope="scope">
            <span :title="scope.row.path" class="path-text">{{ scope.row.path }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="60">
          <template slot-scope="scope">
            <el-link style="font-size:12px" :underline="false" type="warning"
              @click="restoreFile(scope.row)">恢复</el-link>
          </template>
        </el-table-column>
      </el-table>

      <el-empty v-else description="暂无文件"></el-empty>
    </el-card>

    <el-dialog :visible.sync="restoreDialogVisible" title="恢复文件" width="500px" :close-on-click-modal="false"
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
  </div>
</template>

<script>
import { Message } from 'element-ui'
import { repositoryService } from '@/services/repository.service'
import FolderSelector from '@/components/FolderSelector.vue'

export default {
  name: 'SnapshotFiles',
  components: {
    FolderSelector
  },
  data() {
    return {
      repository: null,
      snapshotShortId: '',
      files: [],
      restoreDialogVisible: false,
      restoreItem: null,
      restoreForm: {
        path: ''
      }
    }
  },
  methods: {
    goBack() {
      this.$router.go(-1)
    },
    fetchFiles: async function () {
      const repositoryId = this.$route.params.repositoryId
      const snapshotId = this.$route.params.snapshotId
      this.snapshotShortId = this.$route.query.shortId || snapshotId.substring(0, 8)

      this.repository = await repositoryService.getRepository(repositoryId)
      // 使用新的树形结构API端点
      this.files = await repositoryService.getSnapshotSubdirectoryFilesTree(repositoryId, snapshotId, this.$route.query.path)
    },
    loadSubdirectory: async function (tree, treeNode, resolve) {
      const repositoryId = this.$route.params.repositoryId
      const snapshotId = this.$route.params.snapshotId
      try {
        // 移除路径开头的斜杠
        let path = tree.path
        const children = await repositoryService.getSnapshotSubdirectoryFilesTree(
          repositoryId,
          snapshotId,
          path
        )
        resolve(children)
      } catch (error) {
        Message.error('获取子目录文件失败: ' + error.message)
        resolve([])
      }
    },
    restoreFile: function (file) {
      this.restoreItem = file
      this.restoreForm.path = ''
      this.restoreDialogVisible = true
    },
    performRestore: async function () {
      const repositoryId = this.$route.params.repositoryId
      const snapshotId = this.$route.params.snapshotId
      if (!this.repository || !this.restoreItem) {
        return
      }

      try {
        await repositoryService.restoreFile(
          repositoryId,
          snapshotId,
          this.restoreItem.path,
          this.restoreForm.path
        )

        Message.success('恢复成功')
        this.restoreDialogVisible = false
      } catch (error) {
        Message.error('恢复失败: ' + error.message)
      }
    },
    formatFileSize: function (bytes) {
      if (bytes === 0) return '0 Bytes'
      const k = 1024
      const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
      const i = Math.floor(Math.log(bytes) / Math.log(k))
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
    },
  },
  mounted() {
    this.fetchFiles()
  }
}
</script>

<style scoped>
.snapshot-files-container {
  padding: 20px;
  max-width: 90%;
  margin: 0 auto;
}

.page-header {
  margin-bottom: 20px;
  padding-bottom: 10px;
  border-bottom: 1px solid #eee;
}

.dialog-footer {
  text-align: right;
}

.name-wrapper {
  display: inline-flex;
  align-items: center;
  height: 100%;
  max-width: calc(100% - 80px);
  white-space: nowrap;
  overflow: hidden;
}

.name-wrapper i {
  margin-right: 5px;
  flex-shrink: 0;
}

.name-text,
.path-text {
  display: inline-block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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