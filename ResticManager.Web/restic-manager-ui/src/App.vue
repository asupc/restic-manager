<template>
  <div id="app">
    <app-navbar></app-navbar>
    <div class="main-container" v-loading.fullscreen.lock="loading" :element-loading-text="loadingText">
      <router-view />
    </div>
  </div>
</template>

<script>
import AppNavbar from '@/components/AppNavbar.vue';

export default {
  name: "App",
  components: {
    AppNavbar
  },
  computed: {
    loading() {
      return this.$store.getters.loading;
    },
    loadingText() {
      return this.$store.getters.loadingText;
    }
  }
};
</script>

<style>
#app {
  font-family: Avenir, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  color: #2c3e50;
}

.main-container {
  margin-top: 60px;
  /* 为顶部导航栏留出空间 */
  padding: 20px;
}
</style>