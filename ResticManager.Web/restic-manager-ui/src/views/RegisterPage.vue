<template>
  <div class="auth-page">
    <div class="auth-brand">
      <div class="auth-brand__logo">
        <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
          <rect x="2" y="6" width="28" height="22" rx="5" fill="#fff"/>
          <path d="M9 16l4 4 10-10" stroke="#0e7490" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
      <h1 class="auth-brand__title">创建账号</h1>
      <p class="auth-brand__sub">只需一个账号,即可开始保护你的数据</p>

      <div class="auth-illustration">
        <svg viewBox="0 0 400 320" xmlns="http://www.w3.org/2000/svg">
          <!-- 服务器/数据库 -->
          <ellipse cx="200" cy="280" rx="160" ry="20" fill="rgba(0,0,0,0.15)"/>
          <rect x="120" y="120" width="160" height="140" rx="16" fill="#fff" opacity="0.95"/>
          <rect x="120" y="120" width="160" height="30" rx="16" fill="rgba(255,255,255,0.6)"/>
          <circle cx="140" cy="135" r="4" fill="#ef4444"/>
          <circle cx="156" cy="135" r="4" fill="#f59e0b"/>
          <circle cx="172" cy="135" r="4" fill="#22c55e"/>
          <line x1="140" y1="170" x2="260" y2="170" stroke="#cbd5e1" stroke-width="2"/>
          <line x1="140" y1="195" x2="240" y2="195" stroke="#cbd5e1" stroke-width="2"/>
          <line x1="140" y1="220" x2="220" y2="220" stroke="#cbd5e1" stroke-width="2"/>
          <!-- 盾牌 -->
          <g transform="translate(310, 60)">
            <path d="M0 0 L40 12 L40 50 Q40 80 20 90 Q0 80 0 50 Z" fill="#fff" opacity="0.95"/>
            <path d="M10 45 L18 53 L32 35" stroke="#06b6d4" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" fill="none"/>
          </g>
          <!-- 时钟 -->
          <g transform="translate(50, 60)">
            <circle cx="0" cy="0" r="30" fill="#fff" opacity="0.95"/>
            <line x1="0" y1="0" x2="0" y2="-18" stroke="#06b6d4" stroke-width="3" stroke-linecap="round"/>
            <line x1="0" y1="0" x2="12" y2="6" stroke="#06b6d4" stroke-width="3" stroke-linecap="round"/>
            <circle cx="0" cy="0" r="3" fill="#06b6d4"/>
          </g>
          <!-- 箭头连接 -->
          <path d="M85 90 Q120 100 150 130" stroke="rgba(255,255,255,0.5)" stroke-width="2" fill="none" stroke-dasharray="4 4"/>
          <path d="M310 100 Q280 110 250 130" stroke="rgba(255,255,255,0.5)" stroke-width="2" fill="none" stroke-dasharray="4 4"/>
        </svg>
      </div>

      <div class="auth-brand__deco">
        <svg viewBox="0 0 400 400" xmlns="http://www.w3.org/2000/svg">
          <circle cx="80" cy="320" r="120" fill="rgba(255,255,255,0.05)"/>
          <circle cx="320" cy="80" r="160" fill="rgba(255,255,255,0.04)"/>
        </svg>
      </div>
    </div>

    <div class="auth-form">
      <div class="auth-card">
        <h2 class="auth-card__title">开始你的备份之旅 🚀</h2>
        <p class="auth-card__sub">注册新账号以解锁全部功能</p>

        <el-form ref="registerForm" :model="registerForm" :rules="registerRules" size="default" @submit.native.prevent="handleRegister">
          <el-form-item prop="username">
            <el-input v-model="registerForm.username" placeholder="用户名 (3-20 字符)" prefix-icon="el-icon-user"></el-input>
          </el-form-item>
          <el-form-item prop="password">
            <el-input v-model="registerForm.password" type="password" placeholder="密码 (至少 6 位)" prefix-icon="el-icon-lock" show-password></el-input>
          </el-form-item>
          <el-form-item prop="confirmPassword">
            <el-input v-model="registerForm.confirmPassword" type="password" placeholder="确认密码" prefix-icon="el-icon-lock" show-password></el-input>
          </el-form-item>
          <el-button type="primary" :loading="isLoading" @click="handleRegister" class="auth-card__submit" round>
            创建账号
          </el-button>
        </el-form>

        <div class="auth-card__footer">
          已有账号?
          <el-button type="text" class="text-brand" @click="$router.push('/login')">返回登录</el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { authService } from '@/services/auth.service';

export default {
  data() {
    const validateConfirm = (rule, value, callback) => {
      if (value !== this.registerForm.password) callback(new Error('两次输入的密码不一致'));
      else callback();
    };
    return {
      registerForm: { username: '', password: '', confirmPassword: '' },
      registerRules: {
        username: [
          { required: true, message: '请输入用户名', trigger: 'blur' },
          { min: 3, max: 20, message: '用户名长度在 3 到 20 个字符', trigger: 'blur' }
        ],
        password: [
          { required: true, message: '请输入密码', trigger: 'blur' },
          { min: 6, message: '密码长度至少 6 位', trigger: 'blur' }
        ],
        confirmPassword: [
          { required: true, message: '请确认密码', trigger: 'blur' },
          { validator: validateConfirm, trigger: 'blur' }
        ]
      },
      isLoading: false
    };
  },
  methods: {
    handleRegister() {
      this.$refs.registerForm.validate(async valid => {
        if (!valid) return;
        this.isLoading = true;
        try {
          await authService.register(this.registerForm);
          this.$message.success('注册成功,请登录');
          this.$router.push('/login');
        } catch (e) {
          this.$message.error(e.response?.data?.message || '注册失败');
        } finally {
          this.isLoading = false;
        }
      });
    }
  }
};
</script>

<style scoped>
.auth-page { display: flex; min-height: 100vh; background: var(--color-bg-page); }

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
.auth-brand__logo { width: 56px; height: 56px; margin-bottom: 24px; }
.auth-brand__logo svg { width: 100%; height: 100%; }
.auth-brand__title { font-size: 36px; font-weight: 700; margin: 0; letter-spacing: -0.02em; }
.auth-brand__sub { font-size: 16px; opacity: 0.85; margin: 8px 0 32px; }
.auth-illustration { max-width: 400px; margin-top: 20px; position: relative; z-index: 1; }
.auth-brand__deco { position: absolute; inset: 0; pointer-events: 0; }
.auth-brand__deco svg { width: 100%; height: 100%; }

.auth-form {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px;
}
.auth-card { width: 100%; max-width: 400px; }
.auth-card__title { font-size: 28px; font-weight: 700; color: var(--color-text-primary); margin: 0; letter-spacing: -0.01em; }
.auth-card__sub { font-size: 14px; color: var(--color-text-secondary); margin: 8px 0 28px; }
.auth-card .el-form-item { margin-bottom: 18px; }
.auth-card .el-input__inner { height: 42px; }
.auth-card .el-input__inner:focus { border-color: var(--brand-500); }
.auth-card .el-button--primary {
  background: var(--brand-600);
  border: none;
}
.auth-card__submit { width: 100%; height: 44px; font-size: 15px; font-weight: 600; margin-top: 8px; }
.auth-card__footer {
  margin-top: 24px;
  text-align: center;
  font-size: 13px;
  color: var(--color-text-secondary);
}

@media (max-width: 960px) {
  .auth-page { flex-direction: column; }
  .auth-brand { padding: 40px 30px; }
  .auth-brand__title { font-size: 28px; }
  .auth-form { padding: 30px 20px; }
  .auth-illustration { display: none; }
}
</style>