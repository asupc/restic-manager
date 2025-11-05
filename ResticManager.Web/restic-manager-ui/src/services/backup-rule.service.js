import api from "./api";

export const backupRuleService = {
  /**
   * 获取所有备份规则
   */
  async getAllRules() {
    const response = await api.get("/backuprules");
    return response.data;
  },

  /**
   * 根据ID获取备份规则
   */
  async getRuleById(id) {
    const response = await api.get(`/backuprules/${id}`);
    return response.data;
  },

  /**
   * 创建备份规则
   */
  async createRule(ruleData) {
    const response = await api.post("/backuprules", ruleData);
    return response.data;
  },

  /**
   * 更新备份规则
   */
  async updateRule(id, ruleData) {
    const response = await api.put(`/backuprules/${id}`, ruleData);
    return response.data;
  },

  /**
   * 删除备份规则
   */
  async deleteRule(id) {
    await api.delete(`/backuprules/${id}`);
  },
};
