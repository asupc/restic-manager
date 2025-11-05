<template>
    <div class="repository-snapshots-container">
        <div class="page-header">
            <el-page-header @back="goBack" :content="`仓库 ${repository?.name} 的快照`">
            </el-page-header>
        </div>

        <snapshot-list :snapshots="snapshots" :repository-id="$route.params.id" :repository="repository"
            @restore-snapshot="handleRestoreSnapshot" @delete-snapshot="handleDeleteSnapshot">
        </snapshot-list>
    </div>
</template>

<script>
import { Message, MessageBox } from 'element-ui'
import { repositoryService } from '@/services/repository.service'
import SnapshotList from '@/components/SnapshotList.vue'

export default {
    name: 'RepositorySnapshots',
    components: {
        SnapshotList
    },
    data() {
        return {
            repository: null,
            snapshots: [],
        }
    },
    methods: {
        goBack() {
            this.$router.go(-1)
        },
        fetchSnapshots: async function () {
            const repositoryId = this.$route.params.id
            this.repository = await repositoryService.getRepository(repositoryId)
            this.snapshots = await repositoryService.getSnapshots(repositoryId)
        },
        handleRestoreSnapshot: async function (data) {
            const { repositoryId, snapshot, targetPath } = data
            if (!this.repository || !snapshot) {
                return
            }

            try {
                await repositoryService.restoreSnapshot(
                    repositoryId,
                    snapshot.id,
                    targetPath
                )

                Message.success('恢复成功')
            } catch (error) {
                Message.error('恢复失败: ' + error.message)
            }
        },
        handleDeleteSnapshot: async function (data) {
            const { repositoryId, snapshot } = data
            if (!this.repository || !snapshot) {
                return
            }

            try {
                await repositoryService.deleteSnapshot(
                    repositoryId,
                    snapshot.id
                )

                Message.success('删除成功')

                // 重新加载快照列表
                await this.fetchSnapshots()
            } catch (error) {
                Message.error('删除失败: ' + error.message)
            }
        }
    },
    mounted() {
        this.fetchSnapshots()
    }
}
</script>

<style scoped>
.repository-snapshots-container {
    padding: 20px;
    max-width: 90%;
    margin: 0 auto;
}

.page-header {
    margin-bottom: 20px;
    padding-bottom: 10px;
    border-bottom: 1px solid #eee;
}
</style>