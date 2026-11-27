import { taskService } from "../services/task.service";

const DEFAULT_MAX_DURATION = 30 * 60 * 1000; // 30 分钟超时兜底
const NON_RETRYABLE_STATUSES = [400, 401, 403, 404];

/**
 * 轮询单个后台任务直到终态（Succeeded / Failed），resolve 最终的 task 对象。
 *
 * 页面典型用法：
 *   const res = await repositoryService.syncSnapshots(id);   // → { taskId }
 *   const task = await pollTask(res.taskId, { shouldStop: () => this._destroyed });
 *   if (task.status === 'Succeeded') { ... } else { Message.error(task.error) }
 *
 * options:
 *   - interval: 轮询间隔毫秒数，默认 2000
 *   - shouldStop: 返回 true 时以 Error('cancelled') 结束（页面 beforeDestroy 里置 this._destroyed = true）
 *   - onUpdate: 每次拿到任务快照时的回调（进度类场景预留）
 *   - maxDuration: 最大轮询时长毫秒数，默认 30 分钟
 *
 * 网络抖动容忍：连续 3 次请求失败才 reject，遇到 400/401/403/404 直接抛出不重试。
 */
export async function pollTask(
  taskId,
  {
    interval = 2000,
    shouldStop = null,
    onUpdate = null,
    maxDuration = DEFAULT_MAX_DURATION
  } = {}
) {
  const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
  const startTime = Date.now();
  let consecutiveErrors = 0;

  for (;;) {
    if (shouldStop && shouldStop()) {
      throw new Error("cancelled");
    }
    if (maxDuration > 0 && Date.now() - startTime >= maxDuration) {
      throw new Error("任务轮询超时");
    }

    let task;
    try {
      task = await taskService.getTask(taskId);
      consecutiveErrors = 0;
    } catch (e) {
      const status = e && e.response && e.response.status;
      if (NON_RETRYABLE_STATUSES.includes(status)) {
        throw e;
      }
      consecutiveErrors += 1;
      if (consecutiveErrors >= 3) {
        throw e;
      }
      if (shouldStop && shouldStop()) {
        throw new Error("cancelled");
      }
      await wait(interval);
      continue;
    }

    if (shouldStop && shouldStop()) {
      throw new Error("cancelled");
    }

    if (onUpdate) {
      onUpdate(task);
    }
    if (task.status === "Succeeded" || task.status === "Failed") {
      return task;
    }
    await wait(interval);
  }
}
