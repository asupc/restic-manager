<template>
  <div class="login-page">
    <el-card class="login-card">
      <div slot="header" class="login-title">用户登录</div>
      <el-form ref="loginForm" :model="loginForm" :rules="loginRules" label-width="80px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="loginForm.username" placeholder="请输入用户名"></el-input>
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input type="password" v-model="loginForm.password" placeholder="请输入密码" show-password></el-input>
        </el-form-item>
        <el-form-item>
          <el-checkbox v-model="loginForm.rememberMe">记住我</el-checkbox>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleLogin" style="width: 100%;" :loading="isLoading">
            登录
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script>
import { authService } from '@/services/auth.service'

export default {
  data() {
    return {
      loginForm: {
        username: '',
        password: '',
        rememberMe: false
      },
      loginRules: {
        username: [
          { required: true, message: '请输入用户名', trigger: 'blur' }
        ],
        password: [
          { required: true, message: '请输入密码', trigger: 'blur' },
          { min: 6, message: '密码长度至少6位', trigger: 'blur' }
        ]
      },
      isLoading: false
    }
  },
  mounted() {
    // 从本地存储加载记住的账号
    const savedUser = localStorage.getItem('savedUser')
    if (savedUser) {
      this.loginForm = JSON.parse(savedUser)
    }
  },
  methods: {
    handleLogin() {
      this.$refs.loginForm.validate(async (valid) => {
        if (!valid) return

        this.isLoading = true
        try {
          const response = await authService.login(this.loginForm.username, this.loginForm.password)
          // 保存token和用户信息
          localStorage.setItem('token', response.token)
          localStorage.setItem('user', JSON.stringify(response.user))

          // 如果需要记住我，保存表单信息
          if (this.loginForm.rememberMe) {
            localStorage.setItem('savedUser', JSON.stringify(this.loginForm))
          } else {
            localStorage.removeItem('savedUser')
          }
          this.$message.success('登录成功')
          this.$router.push('/')
        } finally {
          this.isLoading = false
        }
      })
    }
  }
}
</script>

<style scoped>
.login-card {
  width: 400px;
  margin: 0 auto;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.login-title {
  text-align: center;
  font-size: 1.5rem;
  font-weight: bold;
}
</style>