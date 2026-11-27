import { Message } from "element-ui";

class HttpErrorHandler {
  /**
   * 安全地从响应体中提取可读错误文本，绝不直接返回对象
   * @param {*} data - 响应数据
   * @returns {string}
   */
  static extractErrorMessage(data) {
    if (!data) return "";
    if (typeof data === "string") {
      return data.trim();
    }
    if (typeof data === "object") {
      if (typeof data.message === "string" && data.message.trim()) {
        return data.message.trim();
      }
      if (typeof data.detail === "string" && data.detail.trim()) {
        return data.detail.trim();
      }
      if (data.errors) {
        if (Array.isArray(data.errors)) {
          const list = data.errors
            .filter(item => typeof item === "string" && item.trim())
            .map(item => item.trim());
          if (list.length > 0) return list.join("；");
        } else if (typeof data.errors === "object") {
          const collected = [];
          Object.keys(data.errors).forEach(key => {
            const val = data.errors[key];
            if (Array.isArray(val)) {
              val.forEach(item => {
                if (typeof item === "string" && item.trim()) {
                  collected.push(item.trim());
                }
              });
            } else if (typeof val === "string" && val.trim()) {
              collected.push(val.trim());
            }
          });
          if (collected.length > 0) return collected.join("；");
        }
      }
      if (typeof data.title === "string" && data.title.trim()) {
        return data.title.trim();
      }
    }
    return "";
  }

  /**
   * 统一处理API错误
   * @param {Error} error - 错误对象
   * @param {string} context - 错误上下文（如"获取用户信息"）
   */
  static handleApiError(error, context = "") {
    if (!error) return;

    // 静默请求跳过全局错误弹窗
    if (error.config && error.config.silent) {
      return;
    }

    // 优先处理 401：非登录接口已在 api.js 中处理跳转，直接静默返回
    if (error.response && error.response.status === 401) {
      const reqUrl = (error.config && error.config.url) || "";
      const isLoginEndpoint = reqUrl.includes("/auth/login");
      if (!isLoginEndpoint) {
        return;
      }
    }

    // 网络错误
    if (error.code === "ERR_NETWORK") {
      const message = context
        ? `${context}失败：网络错误，请检查网络连接`
        : "网络错误，请检查网络连接";
      Message.error(message);
      return;
    }

    // HTTP状态码错误
    if (error.response) {
      const { status, data } = error.response;
      const serverMsg = HttpErrorHandler.extractErrorMessage(data);

      if (serverMsg) {
        Message.error(context ? `${context}失败：${serverMsg}` : serverMsg);
        return;
      }

      switch (status) {
        case 400:
          Message.error(
            context ? `${context}失败：请求参数错误` : "请求参数错误"
          );
          break;
        case 401:
          Message.error(
            context ? `${context}失败：用户名或密码错误` : "用户名或密码错误"
          );
          break;
        case 403:
          Message.error(
            context
              ? `${context}失败：没有权限执行此操作`
              : "没有权限执行此操作"
          );
          break;
        case 404:
          Message.error(
            context ? `${context}失败：请求的资源不存在` : "请求的资源不存在"
          );
          break;
        case 500:
          Message.error(
            context ? `${context}失败：服务器内部错误` : "服务器内部错误"
          );
          break;
        case 502:
        case 503:
        case 504:
          Message.error(
            context ? `${context}失败：服务器暂时不可用` : "服务器暂时不可用"
          );
          break;
        default:
          Message.error(
            context
              ? `${context}失败：未知错误(${status})`
              : `未知错误(${status})`
          );
      }
    } else if (error.request) {
      // 请求已发出但没有收到响应
      const message = context
        ? `${context}失败：服务器无响应，请稍后重试`
        : "服务器无响应，请稍后重试";
      Message.error(message);
    } else {
      // 其他错误
      const fallback = (error && error.message) || "未知错误";
      const message = context ? `${context}失败：${fallback}` : fallback;
      Message.error(message);
    }
  }
}

export default HttpErrorHandler;
