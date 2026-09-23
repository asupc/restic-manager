<template>
  <div id="app">
    <router-view v-if="$route.path === '/login' || $route.path === '/register'" />
    <div v-else class="app-layout">
      <app-navbar :collapsed="sidebarCollapsed" @toggle="sidebarCollapsed = !sidebarCollapsed" />
      <main class="app-main" :class="{ 'app-main--expanded': sidebarCollapsed }">
        <div class="app-content" v-loading.fullscreen.lock="loading" :element-loading-text="loadingText">
          <transition name="page-fade" mode="out-in">
            <router-view />
          </transition>
        </div>
      </main>
    </div>
  </div>
</template>

<script>
import AppNavbar from '@/components/AppNavbar.vue';

export default {
  name: 'App',
  components: { AppNavbar },
  data() {
    return {
      sidebarCollapsed: false
    };
  },
  computed: {
    loading() {
      return this.$store.getters.loading;
    },
    loadingText() {
      return this.$store.getters.loadingText;
    }
  },
  mounted() {
    this.syncTaskPolling();
  },
  watch: {
    '$route'(to) {
      this.syncTaskPolling(to);
    }
  },
  methods: {
    // 登录后启动后台任务全局轮询（侧边栏角标 + 完成通知），离开业务页/登出时停止
    syncTaskPolling(to) {
      const path = to ? to.path : this.$route.path;
      const isAuthPage = path === '/login' || path === '/register';
      if (!isAuthPage && localStorage.getItem('token')) {
        this.$store.dispatch('tasks/start');
      } else {
        this.$store.dispatch('tasks/stop');
      }
    }
  }
};
</script>

<style>
/* 全局样式集中在 src/styles/theme.css */
</style>