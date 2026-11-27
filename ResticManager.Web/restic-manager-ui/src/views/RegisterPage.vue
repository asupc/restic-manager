<template>
  <div class="auth-page">
    <!-- 左侧精致品牌展示区 -->
    <div class="auth-brand">
      <div class="auth-brand__grid"></div>
      <div class="auth-brand__glow auth-brand__glow--primary"></div>
      <div class="auth-brand__glow auth-brand__glow--secondary"></div>

      <div class="auth-brand__content">
        <div class="auth-brand__badge">
          <span class="auth-brand__badge-dot"></span>
          <span>Restic Backup Orchestrator</span>
        </div>

        <div class="auth-brand__header">
          <div class="auth-brand__logo">
            <svg viewBox="0 0 36 36" fill="none" xmlns="http://www.w3.org/2000/svg">
              <rect x="2" y="6" width="32" height="24" rx="7" fill="currentColor" fill-opacity="0.16" stroke="currentColor" stroke-width="2"/>
              <path d="M11 18.5l4.5 4.5 10-10.5" stroke="currentColor" stroke-width="2.8" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </div>
          <div>
            <h1 class="auth-brand__title">ResticManager</h1>
            <p class="auth-brand__sub">初始化管理员账号 · 开启自动化数据防护</p>
          </div>
        </div>

        <div class="auth-feature-grid">
          <div class="auth-feature-card">
            <span class="auth-feature-card__icon"><i class="el-icon-lock"></i></span>
            <div class="auth-feature-card__body">
              <div class="auth-feature-card__title">端到端加密</div>
              <div class="auth-feature-card__desc">AES-256 + Poly1305 工业级加密，保障云端与异地存储安全</div>
            </div>
          </div>

          <div class="auth-feature-card">
            <span class="auth-feature-card__icon"><i class="el-icon-cpu"></i></span>
            <div class="auth-feature-card__body">
              <div class="auth-feature-card__title">增量去重</div>
              <div class="auth-feature-card__desc">智能分块去重与高效压缩，每次增量备份仅上传变更数据</div>
            </div>
          </div>

          <div class="auth-feature-card">
            <span class="auth-feature-card__icon"><i class="el-icon-cloudy"></i></span>
            <div class="auth-feature-card__body">
              <div class="auth-feature-card__title">多云后端</div>
              <div class="auth-feature-card__desc">兼容本地存储、SFTP、REST、S3、B2 与 rclone 多种后端</div>
            </div>
          </div>

          <div class="auth-feature-card">
            <span class="auth-feature-card__icon"><i class="el-icon-time"></i></span>
            <div class="auth-feature-card__body">
              <div class="auth-feature-card__title">自动化调度</div>
              <div class="auth-feature-card__desc">可视化 Cron 备份编排、健康校验与细粒度文件版本恢复</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 右侧居中玻璃态表单卡片 -->
    <div class="auth-form">
      <div class="auth-card">
        <div class="auth-card__eyebrow">初始化控制台</div>
        <h2 class="auth-card__title">创建账号</h2>
        <p class="auth-card__sub">设置你的管理员凭据，立即开始管理备份仓库</p>

        <el-form
          ref="registerForm"
          :model="registerForm"
          :rules="registerRules"
          size="default"
          @submit.native.prevent="handleRegister"
        >
          <el-form-item prop="username">
            <el-input
              v-model="registerForm.username"
              placeholder="用户名（3-20 个字符）"
              prefix-icon="el-icon-user"
              autocomplete="username"
            ></el-input>
          </el-form-item>
          <el-form-item prop="password">
            <el-input
              v-model="registerForm.password"
              type="password"
              placeholder="密码（至少 6 位）"
              prefix-icon="el-icon-lock"
              show-password
              autocomplete="new-password"
            ></el-input>
          </el-form-item>
          <el-form-item prop="confirmPassword">
            <el-input
              v-model="registerForm.confirmPassword"
              type="password"
              placeholder="再次输入密码以确认"
              prefix-icon="el-icon-lock"
              show-password
              autocomplete="new-password"
            ></el-input>
          </el-form-item>
          <el-button
            type="primary"
            native-type="submit"
            :loading="isLoading"
            @click="handleRegister"
            class="auth-card__submit"
          >
            创建账号
          </el-button>
        </el-form>

        <div class="auth-card__footer">
          <span>已有账号？</span>
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
          await authService.register({
            username: this.registerForm.username,
            password: this.registerForm.password,
            email: this.registerForm.email
          });
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
.auth-page {
  display: flex;
  min-height: 100vh;
  background: var(--color-bg-page);
  background-image: var(--ambient-glow);
}

/* 左侧品牌展示区 */
.auth-brand {
  flex: 1.15;
  position: relative;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 56px 52px;
  color: var(--color-text-on-brand);
  background: linear-gradient(145deg, var(--brand-900) 0%, var(--brand-800) 52%, var(--brand-700) 100%);
  border-right: 1px solid var(--color-border);
}

.auth-brand__grid {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(255, 255, 255, 0.06) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.06) 1px, transparent 1px);
  background-size: 36px 36px;
  mask-image: radial-gradient(ellipse at 35% 40%, rgba(0, 0, 0, 0.9), transparent 78%);
  pointer-events: none;
}

.auth-brand__glow {
  position: absolute;
  border-radius: 50%;
  filter: blur(72px);
  pointer-events: none;
}

.auth-brand__glow--primary {
  width: 360px;
  height: 360px;
  top: -80px;
  left: -60px;
  background: var(--brand-500);
  opacity: 0.28;
}

.auth-brand__glow--secondary {
  width: 300px;
  height: 300px;
  bottom: -90px;
  right: -40px;
  background: var(--info-500);
  opacity: 0.22;
}

.auth-brand__content {
  position: relative;
  z-index: 1;
  width: 100%;
  max-width: 540px;
}

.auth-brand__badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 5px 12px;
  border-radius: var(--radius-full);
  background: rgba(255, 255, 255, 0.12);
  border: 1px solid rgba(255, 255, 255, 0.2);
  font-size: var(--font-xs);
  font-weight: 600;
  letter-spacing: 0.03em;
  margin-bottom: 24px;
}

.auth-brand__badge-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--success-500);
  box-shadow: 0 0 8px var(--success-500);
}

.auth-brand__header {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 36px;
}

.auth-brand__logo {
  width: 54px;
  height: 54px;
  flex-shrink: 0;
  color: var(--color-text-on-brand);
}

.auth-brand__logo svg {
  width: 100%;
  height: 100%;
}

.auth-brand__title {
  margin: 0;
  font-size: var(--font-4xl);
  font-weight: 700;
  letter-spacing: -0.025em;
  line-height: 1.15;
}

.auth-brand__sub {
  margin: 6px 0 0;
  font-size: var(--font-md);
  opacity: 0.85;
}

/* 核心特性卡片网格 */
.auth-feature-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
}

.auth-feature-card {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 16px;
  border-radius: var(--radius-lg);
  background: rgba(255, 255, 255, 0.09);
  border: 1px solid rgba(255, 255, 255, 0.16);
  backdrop-filter: blur(10px);
  transition: transform var(--transition-fast), background-color var(--transition-fast), border-color var(--transition-fast);
}

.auth-feature-card:hover {
  transform: translateY(-2px);
  background: rgba(255, 255, 255, 0.14);
  border-color: rgba(255, 255, 255, 0.28);
}

.auth-feature-card__icon {
  width: 34px;
  height: 34px;
  border-radius: var(--radius-md);
  background: rgba(255, 255, 255, 0.16);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  flex-shrink: 0;
}

.auth-feature-card__body {
  min-width: 0;
}

.auth-feature-card__title {
  font-size: var(--font-base);
  font-weight: 600;
  line-height: 1.3;
}

.auth-feature-card__desc {
  margin-top: 4px;
  font-size: var(--font-xs);
  line-height: 1.5;
  opacity: 0.82;
}

/* 右侧玻璃态表单卡片 */
.auth-form {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px 32px;
}

.auth-card {
  width: 100%;
  max-width: 420px;
  padding: 36px 34px 30px;
  border-radius: var(--radius-xl);
  background: var(--color-bg-card);
  border: 1px solid var(--color-border-strong);
  box-shadow: var(--shadow-xl);
  backdrop-filter: blur(16px);
}

.auth-card__eyebrow {
  display: inline-block;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--brand-400);
  margin-bottom: 8px;
}

.auth-card__title {
  margin: 0;
  font-size: var(--font-3xl);
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: -0.02em;
}

.auth-card__sub {
  margin: 8px 0 28px;
  font-size: var(--font-sm);
  color: var(--color-text-secondary);
}

.auth-card >>> .el-form-item {
  margin-bottom: 18px;
}

.auth-card >>> .el-input__inner {
  height: 44px;
  line-height: 44px;
  border-radius: var(--radius-md);
}

.auth-card >>> .el-input__prefix,
.auth-card >>> .el-input__suffix {
  display: flex;
  align-items: center;
}

.auth-card__submit {
  width: 100%;
  height: 44px;
  font-size: var(--font-md);
  font-weight: 600;
  letter-spacing: 0.04em;
  margin-top: 6px;
  box-shadow: var(--shadow-brand);
}

.auth-card__footer {
  margin-top: 22px;
  padding-top: 14px;
  border-top: 1px solid var(--color-border-soft);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  font-size: var(--font-sm);
  color: var(--color-text-secondary);
}

@media (max-width: 960px) {
  .auth-page {
    flex-direction: column;
  }
  .auth-brand {
    flex: none;
    padding: 36px 24px 28px;
    border-right: none;
    border-bottom: 1px solid var(--color-border);
  }
  .auth-brand__header {
    margin-bottom: 0;
  }
  .auth-brand__title {
    font-size: var(--font-2xl);
  }
  .auth-feature-grid {
    display: none;
  }
  .auth-form {
    padding: 28px 18px 40px;
  }
  .auth-card {
    padding: 28px 22px 24px;
  }
}
</style>