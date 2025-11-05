import { repositoryService } from '@/services/repository.service'
import { Message, MessageBox } from 'element-ui'

/**
 * 快照服务，统一封装快照相关操作
 */
export const snapshotService = {
  /**
   * 删除快照
   * @param {number} repositoryId 仓库ID
   * @param {object} snapshot 快照对象
   * @param {function} refreshCallback 删除成功后的刷新回调函数
   */
  async deleteSnapshot(repositoryId, snapshot, refreshCallback) {
    try {
      // 显示确认对话框
      await MessageBox.confirm(
        `确定要删除快照 ${snapshot.short_id} 吗？此操作无法撤销。`,
        '删除快照',
        {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )

      // 执行删除操作
      await repositoryService.deleteSnapshot(repositoryId, snapshot.id)
      
      // 显示成功消息
      Message.success('快照删除成功')
      
      // 执行刷新回调
      if (refreshCallback && typeof refreshCallback === 'function') {
        await refreshCallback()
      }
      
      return true
    } catch (error) {
      // 用户取消操作不显示错误消息
      if (error !== 'cancel' && error !== 'close') {
        Message.error('删除快照失败: ' + (error.message || error))
      }
      return false
    }
  }
}