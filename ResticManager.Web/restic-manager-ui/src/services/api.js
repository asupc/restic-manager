import axios from "axios";
import store from "../store";
import HttpErrorHandler from "./error-handler";

// 用于存储当前正在处理的请求的计数和定时器
let loadingTimer = null;
let requestCount = 0;

// 显示loading
const showLoading = (text = "加载中...") => {
    store.dispatch("updateLoading", { loading: true, text });
};

// 隐藏loading
const hideLoading = () => {
    // 清除定时器
    if (loadingTimer) {
        clearTimeout(loadingTimer);
        loadingTimer = null;
    }

    // 减少请求计数
    requestCount--;

    // 只有当所有请求都完成时才隐藏loading
    if (requestCount <= 0) {
        requestCount = 0;
        store.dispatch("updateLoading", { loading: false });
    }
};

// 开始请求处理
const startRequest = (text) => {
    requestCount++;

    // 如果已经有定时器在运行，则不需要重新设置
    if (loadingTimer) return;

    // 设置100ms延迟显示loading
    loadingTimer = setTimeout(() => {
        showLoading(text);
        loadingTimer = null;
    }, 100);
};

// 结束请求处理
const endRequest = () => {
    hideLoading();
};

// 根据环境设置baseURL - 始终使用相对路径,由 dev server 代理或生产环境同源服务处理
const getBaseURL = () => {
    return "/api";
};

const api = axios.create({
    baseURL: getBaseURL(),
    timeout: 60000, // 60秒
});

api.interceptors.request.use(
    (config) => {
        const isAuthEndpoint =
            config.url.includes("/auth/login") ||
            config.url.includes("/auth/register");
        const token = localStorage.getItem("token");

        if (token && !isAuthEndpoint) {
            config.headers.Authorization = `Bearer ${token}`;
        }

        // 开始请求，设置loading
        // 可以通过 config.loadingText 设置自定义loading文本
        // 传入 { loading: false } 可跳过全局全屏 loading（后台任务提交/轮询请求专用）
        if (config.loading !== false) {
            startRequest(config.loadingText);
        }

        return config;
    },
    (error) => {
        // 请求出错，结束loading
        endRequest();
        return Promise.reject(error);
    }
);

api.interceptors.response.use(
    (response) => {
        // 响应成功，结束loading
        endRequest();
        return response;
    },
    (error) => {
        // 响应出错，结束loading
        endRequest();

        if (error.response && error.response.status === 401) {
            const isLoginEndpoint = error.config.url.includes("/auth/login");
            if (!isLoginEndpoint) {
                localStorage.removeItem("token");
                window.location.href = "/login";
            }
        }
        HttpErrorHandler.handleApiError(error);
        return Promise.reject(error);
    }
);

export default api;
