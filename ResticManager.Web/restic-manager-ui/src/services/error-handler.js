import { Message } from "element-ui";

class HttpErrorHandler {
  /**
   * 统一处理API错误
   * @param {Error} error - 错误对象
   * @param {string} context - 错误上下文（如"获取用户信息"）
   */
  static handleApiError(error, context = "") {
    // 网络错误
    if (error.code === "ERR_NETWORK") {
      const message = context
        ? `${context}失败：网络错误，请检查网络连接`
        : "网络错误，请检查网络连接";
      Message.error(message);
      return;
    }

    if (error && error.response && error.response.data) {
      Message.error(error.response.data);
      return;
    }

    // HTTP状态码错误
    if (error.response) {
      const { status, data } = error.response;

      switch (status) {
        case 400:
          Message.error(
            context ? `${context}失败：请求参数错误` : "请求参数错误"
          );
          break;
        case 401:
          // 401错误已经在api.js中处理
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
          Message.error(error.response.data.detail);
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
      const message = context
        ? `${context}失败：${error.message || "未知错误"}`
        : error.message || "未知错误";
      Message.error(message);
    }
  }
}

export default HttpErrorHandler;
