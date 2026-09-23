import api from "./api";

// 定时维护任务设置服务：列出/更新各维护类型的开关与执行计划
export const maintenanceService = {
    // 全部维护配置（含 nextFireTime 下次执行时间，禁用时为 null）
    async getMaintenanceSettings() {
        const response = await api.get("/maintenancesettings");
        return response.data;
    },

    // 更新单项配置（isEnabled + cronExpression），cron 无效时后端返回 400
    async updateMaintenanceSetting(id, payload) {
        const response = await api.put(`/maintenancesettings/${id}`, payload);
        return response.data;
    }
};
