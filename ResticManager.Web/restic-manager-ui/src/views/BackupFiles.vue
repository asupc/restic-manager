<template>
    <div class="backup-files">
        <el-container style="padding: 20px;">
            <el-page-header :content="`备份文件 - ${taskName || '未知任务'}`" @back="handleBack"></el-page-header>

            <!-- 操作区域 -->
            <el-row :gutter="20" style="margin-top: 20px; margin-bottom: 20px;">
                <el-col :span="8">
                    <el-select v-model="selectedSnapshot" placeholder="选择快照" @change="loadFilesBySnapshot" clearable
                        filterable style="width: 100%">
                        <el-option v-for="snapshot in snapshots" :key="snapshot.id"
                            :label="`快照 ${snapshot.id.substring(0, 8)}... (${formatDate(snapshot.time)})`"
                            :value="snapshot.id">
                            <div style="display: flex; justify-content: space-between; width: 100%;">
                                <span>快照 {{ snapshot.id.substring(0, 8) }}...</span>
                                <span style="color: #999; font-size: 12px;">{{ formatDate(snapshot.time) }}</span>
                            </div>
                        </el-option>
                    </el-select>
                </el-col>
                <el-col :span="10">
                    <el-input v-model="fileSearch" placeholder="搜索文件名、路径..." clearable @input="handleFileSearch"
                        prefix-icon="el-icon-search" style="width: 100%">
                    </el-input>
                </el-col>
            </el-row>

            <!-- 文件列表 -->
            <el-card shadow="never">
                <!-- 表格 -->
                <el-table :data="paginatedFiles" border v-loading="isLoading" element-loading-text="加载文件中..."
                    style="width: 100%" :row-class-name="tableRowClassName" max-height="600">
                    <el-table-column prop="path" label="文件路径" min-width="400">
                        <template #default="scope">
                            <div style="display: flex; align-items: center;">
                                <i :class="getFileIcon(scope.row.path)" style="margin-right: 8px; color: #999;"></i>
                                <el-tooltip :content="scope.row.path" placement="top">
                                    <span>{{ truncatePath(scope.row.path) }}</span>
                                </el-tooltip>
                            </div>
                        </template>
                    </el-table-column>
                    <el-table-column prop="size" label="大小" width="140" align="right">
                        <template #default="scope">
                            <span :title="`${scope.row.size} 字节`">{{ formatSize(scope.row.size) }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column prop="modifiedAt" label="修改时间" width="180">
                        <template #default="scope">
                            <div style="display: flex; flex-direction: column;">
                                <span>{{ formatDate(scope.row.modifiedAt) }}</span>
                                <small style="color: #999;">{{ getTimeAgo(scope.row.modifiedAt) }}</small>
                            </div>
                        </template>
                    </el-table-column>
                    <el-table-column label="操作" width="140" align="center">
                        <template #default="scope">
                            <el-button size="small" type="success" plain @click="previewFile(scope.row)"
                                :disabled="!isPreviewSupported(scope.row.path)"
                                :title="isPreviewSupported(scope.row.path) ? '预览文件' : '暂不支持预览'">
                                <i class="el-icon-view"></i>
                            </el-button>
                            <el-button size="small" type="primary" @click="downloadFile(scope.row)">
                                <i class="el-icon-download"></i> 下载
                            </el-button>
                        </template>
                    </el-table-column>
                </el-table>

                <!-- 空状态 -->
                <el-empty v-if="!isLoading && filteredFiles.length === 0" description="暂无文件数据" :image-size="100">
                </el-empty>

                <!-- 分页 -->
                <el-pagination v-if="filteredFiles.length > 0" @size-change="handleSizeChange"
                    @current-change="handleCurrentChange" :current-page="currentPage" :page-sizes="[10, 20, 50, 100]"
                    :page-size="pageSize" layout="total, sizes, prev, pager, next, jumper" :total="filteredFiles.length"
                    style="margin-top: 15px; text-align: right">
                </el-pagination>
            </el-card>
        </el-container>
    </div>
</template>

<script>
import { backupService } from '@/services/backup.service'
import { backupFileService } from '@/services/backup-file.service'

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
            snapshots: [], // 快照列表
            allFiles: [], // 所有文件
            filteredFiles: [], // 筛选后的文件
            // 状态
            isLoading: false,
            selectedSnapshot: '',
            fileSearch: '',
            // 分页
            currentPage: 1,
            pageSize: 10
        }
    },
    computed: {
        // 计算当前页显示的数据
        paginatedFiles() {
            const start = (this.currentPage - 1) * this.pageSize
            const end = start + this.pageSize
            return this.filteredFiles.slice(start, end)
        }
    },
    mounted() {
        this.loadSnapshots()
    },
    methods: {
        // 返回上一页
        handleBack() {
            this.$router.go(-1)
        },

        // 加载快照列表
        async loadSnapshots() {
            this.isLoading = true
            try {
                const files = await backupService.getFilesByTaskId(this.taskId)
                this.allFiles = files

                // 提取唯一快照
                const snapshotMap = {}
                files.forEach(file => {
                    if (!snapshotMap[file.snapshotId]) {
                        snapshotMap[file.snapshotId] = {
                            id: file.snapshotId,
                            time: file.snapshotTime
                        }
                    }
                })
                this.snapshots = Object.values(snapshotMap).sort((a, b) =>
                    new Date(b.time) - new Date(a.time)
                )

                // 默认加载最新快照
                if (this.snapshots.length > 0) {
                    this.selectedSnapshot = this.snapshots[0].id
                    this.loadFilesBySnapshot()
                }
            } finally {
                this.isLoading = false
            }
        },

        // 加载指定快照的文件
        async loadFilesBySnapshot() {
            if (!this.selectedSnapshot) {
                this.allFiles = []
                this.filteredFiles = []
                return
            }

            this.isLoading = true
            try {
                const files = await backupFileService.getFilesByTaskId(this.taskId)
                this.allFiles = files.filter(f => f.snapshotId === this.selectedSnapshot)
                this.filteredFiles = [...this.allFiles]
                // 重置分页
                this.currentPage = 1
            } catch (err) {
                this.$message.error(err.response?.data?.message || err.message)
            } finally {
                this.isLoading = false
            }
        },

        // 搜索文件
        handleFileSearch() {
            if (!this.fileSearch) {
                this.filteredFiles = [...this.allFiles]
                this.currentPage = 1
                return
            }

            const term = this.fileSearch.toLowerCase()
            this.filteredFiles = this.allFiles.filter(file =>
                file.path.toLowerCase().includes(term)
            )
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

        // 下载文件
        async downloadFile(file) {
            await backupFileService.downloadFile(this.taskId, file.id)
            this.$message.success('文件下载成功')
        },

        // 预览文件
        previewFile(file) {
            if (!this.isPreviewSupported(file.path)) {
                this.$message.info('该文件类型暂不支持预览')
                return
            }
            // TODO: 实现文件预览功能
            this.$message.info('正在开发预览功能...')
        },

        // 判断是否支持预览
        isPreviewSupported(filePath) {
            const ext = filePath.toLowerCase().split('.').pop()
            const previewable = ['txt', 'log', 'json', 'xml', 'html', 'css', 'js', 'md']
            return previewable.includes(ext)
        },

        // 获取文件图标
        getFileIcon(filePath) {
            const ext = filePath.toLowerCase().split('.').pop()
            const imageExts = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg']
            const codeExts = ['js', 'ts', 'java', 'cpp', 'c', 'py', 'php', 'rb', 'go']
            const docExts = ['doc', 'docx', 'pdf', 'txt', 'md']

            if (imageExts.includes(ext)) return 'el-icon-picture-outline'
            if (codeExts.includes(ext)) return 'el-icon-tickets'
            if (docExts.includes(ext)) return 'el-icon-document'
            if (ext === 'zip' || ext === 'rar' || ext === '7z') return 'el-icon-folder-opened'
            return 'el-icon-document'
        },

        // 工具函数
        formatDate(dateString) {
            return new Date(dateString).toLocaleString()
        },

        // 相对时间
        getTimeAgo(dateString) {
            const now = new Date()
            const past = new Date(dateString)
            const diffMs = now - past
            const diffSec = Math.floor(diffMs / 1000)
            const diffMin = Math.floor(diffSec / 60)
            const diffHour = Math.floor(diffMin / 60)
            const diffDay = Math.floor(diffHour / 24)

            if (diffSec < 60) return '刚刚'
            if (diffMin < 60) return `${diffMin}分钟前`
            if (diffHour < 24) return `${diffHour}小时前`
            if (diffDay < 7) return `${diffDay}天前`
            return '一周以前'
        },

        formatSize(bytes) {
            if (bytes === 0) return '0 B'
            if (bytes < 1024) return `${bytes} B`
            if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`
            if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(2)} MB`
            return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`
        },

        truncatePath(path) {
            if (path.length <= 60) return path
            return path.substring(0, 35) + '...' + path.substring(path.length - 20)
        },

        // 表格行样式
        tableRowClassName({ row, rowIndex }) {
            if (rowIndex % 2 === 1) {
                return 'table-row-even'
            }
            return ''
        }
    }
}
</script>

<style scoped>
.backup-files {
    min-height: 100vh;
    background-color: #f5f7fa;
}

/* 表格偶数行背景色 */
::v-deep(.table-row-even) {
    background-color: #fafafa;
}

/* 文件图标 */
::v-deep(.el-icon-picture-outline),
::v-deep(.el-icon-tickets),
::v-deep(.el-icon-document),
::v-deep(.el-icon-folder-opened) {
    font-size: 18px;
}

/* 按钮间距 */
::v-deep(.el-button-group .el-button) {
    margin-left: 5px;
}

/* 空状态样式 */
::v-deep(.el-empty__description) {
    font-size: 14px;
    color: #999;
}
</style>