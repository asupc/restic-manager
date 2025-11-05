<template>
  <el-card class="register-card">
    <div slot="header" class="register-title">用户注册</div>
    <el-form ref="registerForm" :model="registerForm" :rules="registerRules" label-width="80px">
      <el-form-item label="用户名" prop="username">
        <el-input v-model="registerForm.username" placeholder="请输入用户名"></el-input>
      </el-form-item>
      <!-- <el-form-item label="邮箱" prop="email">
        <el-input v-model="registerForm.email" placeholder="请输入邮箱"></el-input>
      </el-form-item> -->
      <el-form-item label="密码" prop="password">
        <el-input type="password" v-model="registerForm.password" placeholder="请输入密码（至少6位）" show-password></el-input>
      </el-form-item>
      <el-form-item label="确认密码" prop="confirmPassword">
        <el-input type="password" v-model="registerForm.confirmPassword" placeholder="请再次输入密码" show-password></el-input>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="handleRegister" style="width: 100%;" :loading="isLoading">
          注册
        </el-button>
      </el-form-item>
    </el-form>
  </el-card>
</template>

<script>
import { authService } from '@/services/auth.service'

export default {
  data() {
    return {
      registerForm: {
        username: '',
        email: '',
        password: '',
        confirmPassword: ''
      },
      registerRules: {
        username: [
          { required: true, message: '请输入用户名', trigger: 'blur' },
          { min: 3, max: 20, message: '用户名长度在 3 到 20 个字符', trigger: 'blur' }
        ],
        password: [
          { required: true, message: '请输入密码', trigger: 'blur' },
          { min: 6, message: '密码长度至少6位', trigger: 'blur' }
        ],
        confirmPassword: [
          { required: true, message: '请确认密码', trigger: 'blur' },
          {
            validator: (rule, value, callback) => {
              if (value !== this.registerForm.password) {
                callback(new Error('两次输入的密码不一致'))
              } else {
                callback()
              }
            },
            trigger: 'blur'
          }
        ]
      },
      isLoading: false
    }
  },
  methods: {
    handleRegister() {
      this.$refs.registerForm.validate(async (valid) => {
        if (!valid)
          return

        this.isLoading = true
        try {
          await authService.register(this.registerForm)
          this.$message.success('注册成功，请登录')
          this.$router.push('/login')
        } catch (error) {
          this.$message.error(error.response?.data?.message || '注册失败')
        } finally {
          this.isLoading = false
        }
      })
    }
  }
}
</script>

<style scoped>
.register-card {
  margin: 0 auto;
  width: 500px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.register-title {
  text-align: center;
  font-size: 1.5rem;
  font-weight: bold;
}
</style>