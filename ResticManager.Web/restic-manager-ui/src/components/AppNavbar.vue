<template>
    <el-menu :default-active="activeIndex" class="el-menu-demo" mode="horizontal" :ellipsis="false" router>
        <el-menu-item index="/">
            <i class="el-icon-house"></i>
            首页
        </el-menu-item>
        <el-menu-item index="/tasks">
            <i class="el-icon-tickets"></i>
            任务
        </el-menu-item>
        <el-menu-item index="/rules">
            <i class="el-icon-document"></i>
            规则
        </el-menu-item>
        <el-menu-item index="/repositories">
            <i class="el-icon-box"></i>
            仓库
        </el-menu-item>

        <el-menu-item index="/backup-management">
            <i class="el-icon-folder-opened"></i>
            备份管理
        </el-menu-item>

        <div @click="confirmLogout" class="logout">
            <i class="el-icon-switch-button"></i>
            登出
        </div>

    </el-menu>
</template>

<script>
import { authService } from '@/services/auth.service'

export default {
    name: 'AppNavbar',
    data() {
        return {
            username: ''
        }
    },
    computed: {
        activeIndex() {
            return this.$route.path
        }
    },
    methods: {
        confirmLogout() {
            this.$confirm('确定要登出吗？', '确认登出', {
                confirmButtonText: '确定',
                cancelButtonText: '取消',
                type: 'warning'
            }).then(() => {
                authService.logout()
                this.$router.push('/login')
            }).catch(() => {
                // 用户取消登出，不执行任何操作
            })
        }
    },
    mounted() {
        // Get username from stored user data
        // const user = JSON.parse(localStorage.getItem('user') || '{}')
        // this.username = user.username || '用户'
    }
}
</script>

<style scoped>
.el-menu {
    display: flex;
    align-items: center;
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    z-index: 1000;
}

.logout {
    margin-left: auto;
    cursor: pointer;
    margin-right: 20px;
}
</style>