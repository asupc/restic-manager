import Vue from "vue";
import VueRouter from "vue-router";
import HomePage from "@/views/HomePage.vue";
import LoginPage from "@/views/LoginPage.vue";
import RegisterPage from "@/views/RegisterPage.vue";
import BackupTasks from "@/views/BackupTasks.vue";
import BackupRules from "@/views/BackupRules.vue";
import Repositories from "@/views/Repositories.vue";
import RepositorySnapshots from "@/views/RepositorySnapshots.vue";
import SnapshotFiles from "@/views/SnapshotFiles.vue";
import BackupManagement from "@/views/BackupManagement.vue";
import BackupRecords from "@/views/BackupRecords.vue";
import SystemLogs from "@/views/SystemLogs.vue";
import Settings from "@/views/Settings.vue";
import { authService } from "@/services/auth.service";

Vue.use(VueRouter);

const routes = [
  {
    path: "/",
    name: "Home",
    component: HomePage,
  },
  {
    path: "/login",
    name: "Login",
    component: LoginPage,
  },
  {
    path: "/register",
    name: "Register",
    component: RegisterPage,
  },
  {
    path: "/tasks",
    name: "BackupTasks",
    component: BackupTasks,
  },
  {
    path: "/records",
    name: "BackupRecords",
    component: BackupRecords,
  },
  {
    path: "/rules",
    name: "BackupRules",
    component: BackupRules,
  },
  {
    path: "/backup-management",
    name: "BackupManagement",
    component: BackupManagement,
  },
  {
    path: "/repositories",
    name: "Repositories",
    component: Repositories,
  },
  {
    path: "/repositories/:id/snapshots",
    name: "RepositorySnapshots",
    component: RepositorySnapshots,
    props: true,
  },
  {
    path: "/repositories/:repositoryId/snapshots/:snapshotId/files",
    name: "SnapshotFiles",
    component: SnapshotFiles,
    props: true,
  },
  {
    path: "/logs",
    name: "SystemLogs",
    component: SystemLogs,
  },
  {
    path: "/settings",
    name: "Settings",
    component: Settings,
  },
];

const router = new VueRouter({
  mode: "history",
  base: process.env.BASE_URL,
  routes,
});

// Navigation guard
router.beforeEach(async (to, from, next) => {
  // 检查是否已经有用户存在
  const hasUserResponse = await authService.hasAnyUser();
  const hasUser = hasUserResponse.hasUser;

  // 如果没有用户且不是去注册页面，则跳转到注册页面
  if (!hasUser && to.path !== "/register") {
    return next("/register");
  }

  // 如果已经有用户且尝试访问注册页面，则跳转到登录页面
  if (hasUser && to.path === "/register") {
    return next("/login");
  }

  const publicPages = ["/login", "/register"];
  const authRequired = !publicPages.includes(to.path);
  const loggedIn = localStorage.getItem("user");

  if (authRequired && !loggedIn) {
    return next("/login");
  }

  next();
});

export default router;
