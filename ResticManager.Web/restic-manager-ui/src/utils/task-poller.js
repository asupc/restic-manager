import { taskService } from "../services/task.service";

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
 *
 * 网络抖动容忍：连续 3 次请求失败才 reject，单次失败继续重试。
 */
export async function pollTask(taskId, { interval = 2000, shouldStop = null, onUpdate = null } = {}) {
  const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
  let consecutiveErrors = 0;

  for (;;) {
    if (shouldStop && shouldStop()) {
      throw new Error("cancelled");
    }

    let task;
    try {
      task = await taskService.getTask(taskId);
      consecutiveErrors = 0;
    } catch (e) {
      consecutiveErrors += 1;
      if (consecutiveErrors >= 3) {
        throw e;
      }
      await wait(interval);
      continue;
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
