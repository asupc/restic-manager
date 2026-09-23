<template>
  <div class="auth-page">
    <!-- 左侧品牌区 -->
    <div class="auth-brand">
      <div class="auth-brand__logo">
        <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
          <rect x="2" y="6" width="28" height="22" rx="5" fill="#fff"/>
          <path d="M9 16l4 4 10-10" stroke="#0e7490" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
      <h1 class="auth-brand__title">ResticManager</h1>
      <p class="auth-brand__sub">智能备份管家 · 数据安全的最后一公里</p>

      <ul class="auth-features">
        <li>
          <span class="auth-features__icon"><i class="el-icon-time"></i></span>
          <div>
            <div class="auth-features__title">自动化定时备份</div>
            <div class="auth-features__desc">Cron 表达式灵活配置,告别手工备份</div>
          </div>
        </li>
        <li>
          <span class="auth-features__icon"><i class="el-icon-lock"></i></span>
          <div>
            <div class="auth-features__title">端到端加密</div>
            <div class="auth-features__desc">基于 restic 的工业级加密方案</div>
          </div>
        </li>
        <li>
          <span class="auth-features__icon"><i class="el-icon-view"></i></span>
          <div>
            <div class="auth-features__title">可视化快照对比</div>
            <div class="auth-features__desc">逐文件查看变更,按需恢复到任意版本</div>
          </div>
        </li>
        <li>
          <span class="auth-features__icon"><i class="el-icon-folder"></i></span>
          <div>
            <div class="auth-features__title">多仓库统一管理</div>
            <div class="auth-features__desc">本地、SFTP、S3 等多种后端并存</div>
          </div>
        </li>
      </ul>

      <div class="auth-brand__deco">
        <svg viewBox="0 0 400 400" xmlns="http://www.w3.org/2000/svg">
          <circle cx="80" cy="320" r="120" fill="rgba(255,255,255,0.05)"/>
          <circle cx="320" cy="80" r="160" fill="rgba(255,255,255,0.04)"/>
          <circle cx="350" cy="280" r="80" fill="rgba(255,255,255,0.06)"/>
        </svg>
      </div>
    </div>

    <!-- 右侧表单 -->
    <div class="auth-form">
      <div class="auth-card">
        <h2 class="auth-card__title">欢迎回来</h2>
        <p class="auth-card__sub">登录以管理你的备份任务</p>

        <el-form ref="loginForm" :model="loginForm" :rules="loginRules" size="default" @submit.native.prevent="handleLogin">
          <el-form-item prop="username">
            <el-input v-model="loginForm.username" placeholder="用户名" prefix-icon="el-icon-user"></el-input>
          </el-form-item>
          <el-form-item prop="password">
            <el-input v-model="loginForm.password" type="password" placeholder="密码" prefix-icon="el-icon-lock" show-password></el-input>
          </el-form-item>
          <el-form-item>
            <div class="auth-form__row">
              <el-checkbox v-model="loginForm.rememberMe">记住我</el-checkbox>
              <el-button v-if="!hasUser" type="text" @click="$router.push('/register')" class="text-brand">注册账号</el-button>
            </div>
          </el-form-item>
          <el-button type="primary" :loading="isLoading" @click="handleLogin" class="auth-card__submit" round>
            登 录
          </el-button>
        </el-form>

        <div class="auth-card__footer">
          <i class="el-icon-lock"></i>
          所有数据均加密传输 · 我们不会保存你的密码
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { authService } from '@/services/auth.service';

export default {
  data() {
    return {
      hasUser: true,
      loginForm: { username: '', password: '', rememberMe: false },
      loginRules: {
        username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
        password: [{ required: true, message: '请输入密码', trigger: 'blur' }, { min: 6, message: '密码长度至少 6 位', trigger: 'blur' }]
      },
      isLoading: false
    };
  },
  async mounted() {
    const savedUser = localStorage.getItem('savedUser');
    if (savedUser) {
      try { this.loginForm = JSON.parse(savedUser); } catch (e) { /* 解析失败忽略 */ }
    }
    try {
      const r = await authService.hasAnyUser();
      this.hasUser = r.hasUser;
    } catch (e) { /* 接口失败忽略,默认有用户 */ }
  },
  methods: {
    handleLogin() {
      this.$refs.loginForm.validate(async valid => {
        if (!valid) return;
        this.isLoading = true;
        try {
          const response = await authService.login(this.loginForm.username, this.loginForm.password);
          localStorage.setItem('token', response.token);
          // 登录接口只返回 token,用输入的用户名维护本地用户信息
          localStorage.setItem('user', JSON.stringify({ username: this.loginForm.username }));
          if (this.loginForm.rememberMe) {
            localStorage.setItem('savedUser', JSON.stringify(this.loginForm));
          } else {
            localStorage.removeItem('savedUser');
          }
          this.$message.success('登录成功');
          this.$router.push('/');
        } catch (e) {
          this.$message.error(e.response?.data?.message || '登录失败');
        } finally {
          this.isLoading = false;
        }
      });
    }
  }
};
</script>

<style scoped>
.auth-page {
  display: flex;
  min-height: 100vh;
  background: var(--color-bg-page);
}

/* 品牌区 */
.auth-brand {
  flex: 1.1;
  background: var(--brand-600);
  padding: 60px;
  color: #fff;
  display: flex;
  flex-direction: column;
  justify-content: center;
  position: relative;
  overflow: hidden;
}
.auth-brand__logo {
  width: 56px;
  height: 56px;
  margin-bottom: 24px;
}
.auth-brand__logo svg { width: 100%; height: 100%; }
.auth-brand__title {
  font-size: 36px;
  font-weight: 700;
  margin: 0;
  letter-spacing: -0.02em;
}
.auth-brand__sub {
  font-size: 16px;
  opacity: 0.85;
  margin-top: 8px;
  margin-bottom: 48px;
}

.auth-features {
  list-style: none;
  padding: 0;
  margin: 0;
  position: relative;
  z-index: 1;
}
.auth-features li {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 14px 0;
}
.auth-features__icon {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.18);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  flex-shrink: 0;
}
.auth-features__title {
  font-weight: 600;
  font-size: 15px;
}
.auth-features__desc {
  font-size: 13px;
  opacity: 0.75;
  margin-top: 2px;
}

.auth-brand__deco {
  position: absolute;
  inset: 0;
  pointer-events: none;
}
.auth-brand__deco svg { width: 100%; height: 100%; }

/* 表单区 */
.auth-form {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px;
}
.auth-card {
  width: 100%;
  max-width: 400px;
}
.auth-card__title {
  font-size: 28px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
  letter-spacing: -0.01em;
}
.auth-card__sub {
  font-size: 14px;
  color: var(--color-text-secondary);
  margin: 8px 0 28px;
}
.auth-card .el-form-item { margin-bottom: 18px; }
.auth-card .el-input__inner { height: 42px; }
.auth-card .el-input__inner:focus { border-color: var(--brand-500); }
.auth-card .el-button--primary {
  background: var(--brand-600);
  border: none;
}
.auth-card__submit {
  width: 100%;
  height: 44px;
  font-size: 15px;
  font-weight: 600;
  margin-top: 8px;
}
.auth-form__row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.auth-card__footer {
  margin-top: 24px;
  text-align: center;
  font-size: 12px;
  color: var(--color-text-placeholder);
}
.auth-card__footer i { margin-right: 4px; }

@media (max-width: 960px) {
  .auth-page { flex-direction: column; }
  .auth-brand { padding: 40px 30px; }
  .auth-brand__title { font-size: 28px; }
  .auth-form { padding: 30px 20px; }
  .auth-features { display: none; }
}
</style>