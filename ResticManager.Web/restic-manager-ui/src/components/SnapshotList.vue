<template>
  <el-card>
    <div class="snapshot-toolbar">
      <el-button v-if="!compareMode" type="primary" size="small" @click="enterCompareMode"
        :disabled="snapshots.length < 2">
        比较快照
      </el-button>
      <el-button v-else type="danger" size="small" @click="exitCompareMode">
        取消比较
      </el-button>
      <el-button v-if="compareMode" type="success" size="small" @click="showDiff"
        :disabled="selectedSnapshots.length !== 2" style="margin-left: 10px;">
        显示差异
      </el-button>
    </div>

    <el-alert v-if="selectedSnapshots.length > 0 && compareMode"
      :title="'已选择快照: ' + selectedSnapshots.map(s => new Date(s.time).toLocaleString()).join('  -  ')" type="info"
      show-icon style="margin: 10px 0;">
    </el-alert>
    <el-table :data="snapshots" style="width: 100%" v-if="snapshots.length > 0">
      <el-table-column v-if="compareMode" width="50">
        <template slot-scope="scope">
          <el-checkbox v-model="scope.row.checked" @change="handleSnapshotSelect(scope.row)"
            :disabled="!scope.row.checked && selectedSnapshots.length >= 2">
          </el-checkbox>
        </template>
      </el-table-column>

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
      <el-table-column label="操作" width="250" fixed="right">
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

    <!-- 快照差异抽屉 -->
    <el-drawer :visible.sync="diffDrawerVisible" :title="diffDrawerTitle" :with-header="false" size="60%"
      :close-on-click-modal="false" append-to-body>
      <el-alert v-if="diffError" type="error" :title="diffError" show-icon></el-alert>
      <!-- <el-card class="diff-result-card"> -->
      <div v-if="diffResult && !diffError" class="diff-result">
        <el-card v-if="diffResult.statistics">
          <div slot="header">
            <span>{{ diffDrawerTitle }}</span>
            <el-alert title="快照选择顺序影响比较结果" type="info" show-icon size="small" style="margin-top: 10px;">
            </el-alert>
            <div style="margin-top: 20px; display: flex; justify-content: space-between; align-items: center;">
              <div>
                <el-select v-model="diffTypeFilter" size="mini" placeholder="筛选类型" clearable
                  style="width: 120px; margin-right: 10px;">
                  <el-option label="新增 (+)" value="+"></el-option>
                  <el-option label="删除 (-)" value="-"></el-option>
                  <el-option label="修改 (M)" value="M"></el-option>
                  <!-- <el-option label="类型变更 (T)" value="T"></el-option>
                  <el-option label="元数据变更 (U)" value="U"></el-option> -->
                </el-select>
                <el-input size="mini" v-model="diffSearchKeyword" placeholder="搜索文件路径..." clearable
                  style="width: 300px;" @clear="diffSearchKeyword = ''" @keyup.enter.native="handleDiffSearch">
                  <i slot="prefix" class="el-input__icon el-icon-search"></i>
                </el-input>
              </div>
            </div>

            <div style="margin-top: 10px; display: flex; justify-content: space-between; align-items: center;">
              <el-pagination size="mini" @size-change="handleDiffPageSizeChange" @current-change="handleDiffPageChange"
                :current-page="diffCurrentPage" :page-sizes="[10, 20, 50, 100]" :page-size="diffPageSize"
                layout="total, sizes, prev, pager, next" :total="filteredDiffChanges.length">
              </el-pagination>
            </div>
          </div>
          <el-row :gutter="20">
            <el-col :span="8">
              <div class="stat-item">
                <span class="stat-label">变更文件数:</span>
                <span class="stat-value" style="color:darkorange;">{{ diffResult.statistics.changed_files }}</span>
              </div>
            </el-col>
            <el-col :span="8">
              <div class="stat-item">
                <span class="stat-label">新增文件:</span>
                <span class="stat-value" style="color:green;">{{ diffResult.statistics.added.files }}</span>
              </div>
            </el-col>
            <el-col :span="8">
              <div class="stat-item">
                <span class="stat-label">删除文件:</span>
                <span class="stat-value" style="color: #F00;">{{ diffResult.statistics.removed.files }}</span>
              </div>
            </el-col>
          </el-row>
          <el-row :gutter="20">
            <!-- 可视化差异显示 -->
            <div style="flex: 1; overflow: hidden; display: flex; flex-direction: column; margin-top: 20px;">
              <!-- 添加搜索框和分页组件 -->
              <el-table size="mini" :data="paginatedDiffChanges" style="width: 100%;">
                <el-table-column prop="modifier" label="变更类型" width="120">
                  <template slot-scope="scope">
                    <el-tag :type="getModifierType(scope.row.modifier)">
                      {{ getModifierLabel(scope.row.modifier) }}
                    </el-tag>
                  </template>
                </el-table-column>
                <el-table-column prop="path" label="文件路径"></el-table-column>
              </el-table>
            </div>
          </el-row>
        </el-card>
      </div>
      <div v-else-if="!diffLoading && !diffError">
        <el-alert type="info" title="暂无差异信息" show-icon></el-alert>
      </div>
      <!-- </el-card> -->
    </el-drawer>

  </el-card>
</template>

<script>
import { Message, MessageBox } from 'element-ui'
import FolderSelector from '@/components/FolderSelector.vue'
import { repositoryService } from '@/services/repository.service'

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
      compareMode: false,
      restoreDialogVisible: false,
      deleteDialogVisible: false,
      diffDrawerVisible: false,
      restoreItem: null,
      deleteItem: null,
      diffResult: null,
      diffError: '',
      diffLoading: false,
      selectedSnapshots: [],
      restoreForm: {
        path: ''
      },
      // 添加分页相关数据
      currentPage: 1,
      pageSize: 20,
      // 差异结果分页数据
      diffCurrentPage: 1,
      diffPageSize: 20,
      // 差异结果搜索关键词
      diffSearchKeyword: '',
      // 差异类型筛选
      diffTypeFilter: ''
    }
  },
  computed: {
    diffDrawerTitle() {
      if (this.selectedSnapshots.length === 2) {
        return `快照差异:  ${new Date(this.selectedSnapshots[0].time).toLocaleString()} vs ${new Date(this.selectedSnapshots[1].time).toLocaleString()}`;
      }
      return '快照差异比较';
    },
    // 计算分页后的快照数据
    paginatedSnapshots() {
      const start = (this.currentPage - 1) * this.pageSize;
      const end = start + this.pageSize;
      return this.snapshots.slice(start, end);
    },
    // 计算过滤后的差异变更数据
    filteredDiffChanges() {
      if (!this.diffResult || !this.diffResult.changes) {
        return [];
      }

      let filtered = this.diffResult.changes;

      // 根据类型筛选
      if (this.diffTypeFilter) {
        filtered = filtered.filter(change =>
          change.modifier.includes(this.diffTypeFilter)
        );
      }

      // 根据文件路径过滤变更
      if (this.diffSearchKeyword) {
        filtered = filtered.filter(change =>
          change.path.toLowerCase().includes(this.diffSearchKeyword.toLowerCase())
        );
      }

      return filtered;
    },
    // 计算分页后的差异变更数据
    paginatedDiffChanges() {
      const start = (this.diffCurrentPage - 1) * this.diffPageSize;
      const end = start + this.diffPageSize;
      return this.filteredDiffChanges.slice(start, end);
    }
  },
  methods: {
    // 添加分页相关方法
    handleSizeChange(val) {
      this.pageSize = val;
      this.currentPage = 1;
    },
    handleCurrentChange(val) {
      this.currentPage = val;
    },
    // 差异结果分页方法
    handleDiffPageSizeChange(val) {
      this.diffPageSize = val;
      this.diffCurrentPage = 1;
    },
    handleDiffPageChange(val) {
      this.diffCurrentPage = val;
    },
    // 差异结果搜索方法
    handleDiffSearch() {
      // 重置到第一页
      this.diffCurrentPage = 1;
    },
    enterCompareMode() {
      this.compareMode = true;
      // 初始化快照选择状态
      this.snapshots.forEach(snapshot => {
        this.$set(snapshot, 'checked', false);
      });
      this.selectedSnapshots = [];
    },
    exitCompareMode() {
      this.compareMode = false;
      this.selectedSnapshots = [];
    },
    handleSnapshotSelect(snapshot) {
      if (snapshot.checked) {
        // 添加到选中列表
        if (this.selectedSnapshots.length < 2) {
          // 检查路径是否一致
          if (this.selectedSnapshots.length > 0) {
            const firstSnapshot = this.selectedSnapshots[0];
            if (JSON.stringify(firstSnapshot.paths) !== JSON.stringify(snapshot.paths)) {
              snapshot.checked = false;
              Message.warning('只能比较路径相同的快照');
              return;
            }
          }
          this.selectedSnapshots.push(snapshot);
        } else {
          // 如果已选中两个，取消勾选当前项
          snapshot.checked = false;
          Message.warning('最多只能选择两个快照进行比较');
        }
      } else {
        // 从选中列表中移除
        const index = this.selectedSnapshots.findIndex(s => s.id === snapshot.id);
        if (index > -1) {
          this.selectedSnapshots.splice(index, 1);
        }
      }
    },
    showDiff() {
      if (this.selectedSnapshots.length === 2) {
        // 再次检查路径一致性
        if (JSON.stringify(this.selectedSnapshots[0].paths) !== JSON.stringify(this.selectedSnapshots[1].paths)) {
          Message.error('所选快照的路径不一致，无法比较');
          return;
        }
        this.compareSnapshots(this.selectedSnapshots[0], this.selectedSnapshots[1]);
      }
    },
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
    onDiff(snapshot) {
      // 如果还没有选中快照，则选中当前快照
      if (this.selectedSnapshots.length === 0) {
        this.selectedSnapshots.push(snapshot)
        Message.info('已选择第一个快照，请选择第二个快照进行比较')
        return
      }

      // 如果点击的是已经选中的快照，则取消选择
      if (this.selectedSnapshots.length === 1 && this.selectedSnapshots[0].id === snapshot.id) {
        this.selectedSnapshots = []
        Message.info('已取消选择快照')
        return
      }

      // 检查路径是否一致
      if (JSON.stringify(this.selectedSnapshots[0].paths) !== JSON.stringify(snapshot.paths)) {
        Message.error('只能比较路径相同的快照');
        return;
      }

      // 如果已经选中了一个快照，则进行比较
      if (this.selectedSnapshots.length === 1) {
        this.compareSnapshots(this.selectedSnapshots[0], snapshot)
        // 清除选中状态
        this.selectedSnapshots = []
        return
      }
    },
    async compareSnapshots(snapshot1, snapshot2) {
      this.diffDrawerVisible = true
      this.diffResult = await repositoryService.diffSnapshots(this.repositoryId, snapshot1.id, snapshot2.id)
      // 重置分页和搜索
      this.diffCurrentPage = 1
      this.diffSearchKeyword = ''
      this.diffTypeFilter = ''
    },
    // parseDiffResult() {
    //   try {
    //     const lines = this.diffResult.trim().split('\n');
    //     const changes = [];
    //     let statistics = null;

    //     lines.forEach(line => {
    //       if (!line) return;

    //       try {
    //         const item = JSON.parse(line);
    //         if (item.message_type === 'change') {
    //           changes.push(item);
    //         } else if (item.message_type === 'statistics') {
    //           statistics = item;
    //         }
    //       } catch (e) {
    //         // 忽略解析错误的行
    //       }
    //     });

    //     this.parsedDiffData = changes;
    //     this.diffStatistics = statistics;
    //   } catch (e) {
    //     console.error('解析差异结果失败:', e);
    //   }
    // },
    getModifierType(modifier) {
      if (modifier.includes('+')) return 'success'; // 新增
      if (modifier.includes('-')) return 'danger';  // 删除
      if (modifier.includes('M')) return 'warning'; // 修改
      if (modifier.includes('T')) return 'info';    // 类型变更
      if (modifier.includes('U')) return 'info';    // 元数据变更
      if (modifier.includes('?')) return 'info';    // 位腐坏
      return 'info';
    },
    getModifierLabel(modifier) {
      const labels = {
        '+': '新增',
        '-': '删除',
        'M': '修改',
        'T': '类型变更',
        'U': '元数据变更',
        '?': '位腐坏'
      };

      return modifier.split('').map(char => labels[char] || char).join(',');
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

      if (durationMs < 0) {
        return '无效时间'
      }

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
    },
    isSelected(snapshot) {
      return this.selectedSnapshots.some(s => s.id === snapshot.id)
    }
  },
  beforeDestroy() {
    // 清理添加的属性
    this.snapshots.forEach(snapshot => {
      if ('checked' in snapshot) {
        this.$delete(snapshot, 'checked');
      }
    });
  }
}
</script>

<style scoped>
.snapshot-toolbar {
  margin-bottom: 15px;
}

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

.diff-result-card {
  height: 100%;
  overflow-y: auto;
  flex: 1;
  display: flex;
  flex-direction: column;
}

.diff-result-card ::v-deep .el-card__body {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
}

.diff-result {
  flex: 1;
  display: flex;
  flex-direction: column;
  height: 100%;
}

.diff-result>.el-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.diff-result>.el-card ::v-deep .el-card__body {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}

.diff-drawer-content {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.stat-item {
  display: flex;
  justify-content: space-between;
}

.stat-label {
  color: #606266;
}

.stat-value {
  color: #909399;
}
</style>