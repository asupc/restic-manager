<template>
  <div class="backup-management-container">
    <div class="page-header">
      <h2>备份管理</h2>
    </div>

    <el-card class="repository-selection-card">
      <el-row :gutter="10">
        <el-col :span="6">
          <el-select v-model="selectedRepository" placeholder="请选择仓库" @change="loadSnapshots" style="width: 100%;">
            <el-option v-for="repo in repositories" :key="repo.id" :label="repo.name" :value="repo.id">
            </el-option>
          </el-select>
        </el-col>
      </el-row>
    </el-card>

    <div style="margin-top: 20px;">
      <snapshot-list :snapshots="snapshots" :repository-id="selectedRepository" :show-details="true">
      </snapshot-list>
    </div>
  </div>
</template>

<script>
import { Message } from 'element-ui'
import { repositoryService } from '@/services/repository.service'
import SnapshotList from '@/components/SnapshotList.vue'

export default {
  name: 'BackupManagement',
  components: {
    // FolderSelector,
    SnapshotList
  },
  data() {
    return {
      repositories: [],
      selectedRepository: '',
      snapshots: []
    }
  },
  methods: {
    fetchRepositories: async function () {
      try {
        this.repositories = await repositoryService.getRepositories()
        if (this.repositories.length > 0 && !this.selectedRepository) {
          this.selectedRepository = this.repositories[0].id
          this.loadSnapshots()
        }
      } catch (error) {
        Message.error('获取仓库列表失败: ' + error.message)
      }
    },
    loadSnapshots: async function () {
      if (!this.selectedRepository) {
        return
      }
      this.snapshots = await repositoryService.getSnapshots(this.selectedRepository)

    }
  },
  mounted() {
    this.fetchRepositories()
  }
}
</script>

<style scoped>
.backup-management-container {
  max-width: 90%;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}
</style>