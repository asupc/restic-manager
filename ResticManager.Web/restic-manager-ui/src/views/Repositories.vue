<template>
    <div class="repositories-container">
        <div class="page-header">
            <h1>仓库管理</h1>
            <div>
                <el-button type="primary" size="small" @click="openCreateDialog" icon="el-icon-plus">
                    创建仓库
                </el-button>
            </div>
        </div>

        <el-card>
            <el-table :data="repositories" style="width: 100%" v-if="repositories.length > 0">
                <el-table-column prop="name" label="名称"></el-table-column>
                <el-table-column prop="path" label="路径"></el-table-column>
                <el-table-column prop="type" label="类型"></el-table-column>
                <el-table-column prop="createdAt" label="创建时间" width="180">
                    <template slot-scope="scope">
                        {{ new Date(scope.row.createdAt).toLocaleString() }}
                    </template>
                </el-table-column>
                <el-table-column label="统计信息" width="200">
                    <template slot-scope="scope">
                        <div v-if="scope.row.stats">
                            <div>大小: {{ formatFileSize(scope.row.stats.total_size) }}</div>
                            <div>文件数: {{ scope.row.stats.total_file_count }}</div>
                            <div>快照数: {{ scope.row.stats.snapshots_count }}</div>
                        </div>
                        <div v-else-if="scope.row.isInitialized && scope.row.loadingStats">
                            <i class="el-icon-loading"></i> 加载中...
                        </div>
                        <div v-else-if="scope.row.isInitialized">
                            <el-button size="mini" @click="loadStats(scope.row)" disabled>自动加载</el-button>
                        </div>
                        <div v-else>
                            <el-tag type="danger" size="mini">未初始化</el-tag>
                        </div>
                    </template>
                </el-table-column>
                <el-table-column label="操作" width="250">
                    <template slot-scope="scope">
                        <el-button v-if="!scope.row.isInitialized" size="mini" @click="initializeRepository(scope.row)"
                                   :loading="scope.row.initializing">
                            {{ scope.row.isInitialized ? "重新初始化" : "初始化" }}
                        </el-button>
                        <el-button v-if="!scope.row.isInitialized" size="mini" @click="validateRepository(scope.row)"
                                   :loading="scope.row.validating">
                            验证
                        </el-button>
                        <el-button v-if="scope.row.isInitialized" size="mini" @click="viewSnapshots(scope.row)">查看快照</el-button>
                        <el-button size="mini" type="danger" @click="deleteRepository(scope.row)">删除</el-button>
                    </template>
                </el-table-column>
            </el-table>
            <el-empty v-else description="暂无仓库"></el-empty>
        </el-card>

        <el-dialog :visible.sync="dialogVisible" :title="editingRepository ? '编辑仓库' : '创建仓库'" width="600px"
                   :close-on-click-modal="false" append-to-body>
            <el-form :model="form" :rules="rules" ref="formRef" label-position="left" label-width="120px">
                <el-form-item label="名称" prop="name">
                    <el-input v-model="form.name" placeholder="请输入仓库名称"></el-input>
                </el-form-item>
                <el-form-item label="路径" prop="path">
                    <folder-selector v-model="form.path" title="选择仓库路径" placeholder="请选择或输入仓库路径">
                    </folder-selector>
                    <div class="form-tip">
                        <i class="el-icon-info"></i>
                        选择仓库的存储路径
                    </div>
                </el-form-item>
                <el-form-item label="密码" prop="password">
                    <el-input v-model="form.password" type="password" show-password placeholder="请输入仓库密码"></el-input>
                    <div class="form-tip">
                        <i class="el-icon-info"></i>
                        用于加密仓库数据的密码
                    </div>
                </el-form-item>
                <!-- <el-form-item label="类型" prop="type">
                  <el-select v-model="form.type" placeholder="选择类型">
                    <el-option label="本地" value="local"></el-option>
                    <el-option label="SFTP" value="sftp"></el-option>
                    <el-option label="S3" value="s3"></el-option>
                    <el-option label="Azure" value="azure"></el-option>
                    <el-option label="Google Cloud" value="gcs"></el-option>
                  </el-select>
                  <div class="form-tip">
                    <i class="el-icon-info"></i>
                    选择仓库的存储类型
                  </div>
                </el-form-item> -->
            </el-form>
            <span slot="footer" class="dialog-footer">
                <el-button @click="dialogVisible = false">取消</el-button>
                <el-button type="primary" @click="saveRepository">确认</el-button>
            </span>
        </el-dialog>
    </div>
</template>

<script>
    import { Message, MessageBox } from "element-ui";
    import { repositoryService } from "@/services/repository.service";
    import FolderSelector from "@/components/FolderSelector.vue";

    export default {
        name: "Repositories",
        components: {
            FolderSelector,
        },
        data() {
            return {
                repositories: [],
                dialogVisible: false,
                editingRepository: null,
                form: {
                    name: "",
                    path: "",
                    password: "",
                    type: "local",
                },
                rules: {
                    name: [{ required: true, message: "请输入仓库名称", trigger: "blur" }],
                    path: [{ required: true, message: "请输入仓库路径", trigger: "blur" }],
                    password: [
                        { required: true, message: "请输入仓库密码", trigger: "blur" },
                    ],
                },
            };
        },
        methods: {
            fetchRepositories: async function () {
                this.repositories = await repositoryService.getRepositories();
                // 自动加载所有已初始化仓库的统计信息
                this.loadAllStats();
            },
            openCreateDialog: function () {
                this.editingRepository = null;
                this.dialogVisible = true;
                this.form = {
                    name: "",
                    path: "",
                    password: "",
                    type: "local",
                };
            },
            saveRepository: async function () {
                try {
                    const valid = await this.$refs.formRef.validate();
                    if (!valid) return;

                    if (this.editingRepository) {
                        await repositoryService.updateRepository(
                            this.editingRepository.id,
                            this.form
                        );
                        Message.success("仓库更新成功");
                    } else {
                        await repositoryService.createRepository(this.form);
                        Message.success("仓库创建成功");
                    }

                    this.dialogVisible = false;
                    this.fetchRepositories();
                } catch (error) {
                    Message.error(error.response?.data?.message || error.message);
                }
            },
            deleteRepository: async function (repo) {
                await MessageBox.confirm(`确定要删除仓库 "${repo.name}" 吗？`, "警告", {
                    confirmButtonText: "确定",
                    cancelButtonText: "取消",
                    type: "warning",
                });
                await repositoryService.deleteRepository(repo.id);
                Message.success("仓库删除成功");
                this.fetchRepositories();
            },
            initializeRepository: async function (repo) {
                try {
                    this.$set(repo, "initializing", true);
                    await repositoryService.initializeRepository(repo.id);
                    Message.success("仓库初始化成功");
                    this.fetchRepositories();
                } finally {
                    this.$set(repo, "initializing", false);
                }
            },
            validateRepository: async function (repo) {
                try {
                    this.$set(repo, "validating", true);
                    const result = await repositoryService.validateRepository(repo.id);
                    if (result.valid) {
                        Message.success("仓库验证成功");
                    } else {
                        Message.error("仓库验证失败");
                    }
                    this.fetchRepositories();
                } catch (error) {
                    Message.error("仓库验证失败: " + error.message);
                } finally {
                    this.$set(repo, "validating", false);
                }
            },
            loadStats: async function (repo) {
                try {
                    this.$set(repo, "loadingStats", true);
                    const stats = await repositoryService.getRepositoryStats(repo.id);
                    this.$set(repo, "stats", stats);
                } finally {
                    this.$set(repo, "loadingStats", false);
                }
            },
            // 新增方法：自动加载所有已初始化仓库的统计信息
            loadAllStats: async function () {
                for (const repo of this.repositories) {
                    if (repo.isInitialized && !repo.stats) {
                        this.loadStats(repo);
                    }
                }
            },
            viewSnapshots: function (repo) {
                this.$router.push(`/repositories/${repo.id}/snapshots`);
            },
            formatFileSize(bytes) {
                if (bytes === 0) return "0 Bytes";
                const k = 1024;
                const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
                const i = Math.floor(Math.log(bytes) / Math.log(k));
                return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
            },
        },
        mounted() {
            this.fetchRepositories();
        },
    };
</script>

<style scoped>
    .repositories-container { max-width: 90%; margin: 0 auto; }

    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding-bottom: 10px; border-bottom: 1px solid #eee; }

    .dialog-footer { text-align: right; }

    .form-tip { margin-top: 5px; font-size: 12px; color: #909399; }

        .form-tip i { margin-right: 4px; }
</style>