import api from "./api";

export const systemLogService = {
  /**
   * 分页查询系统日志
   * @param {object} params 查询参数
   * @param {number} params.page 页码
   * @param {number} params.pageSize 每页大小
   * @param {string|null} params.level 最低日志级别（该级别及以上）
   * @param {string|null} params.keyword 关键词（匹配消息/类别/异常）
   * @param {string|null} params.startDate 起始日期（yyyy-MM-dd）
   * @param {string|null} params.endDate 结束日期（yyyy-MM-dd）
   */
  async getLogsPaged({ page = 1, pageSize = 20, level = null, keyword = null, startDate = null, endDate = null } = {}) {
    const params = { page, pageSize };
    if (level) params.level = level;
    if (keyword) params.keyword = keyword;
    if (startDate) params.startDate = startDate;
    if (endDate) params.endDate = endDate;
    const response = await api.get("/systemlogs/paged", { params });
    return response.data;
  }
};
