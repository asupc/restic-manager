import api from "./api";

export const authService = {
  login: async (username, password) => {
    const response = await api.post("/auth/login", { username, password });
    if (response.data.token) {
      localStorage.setItem("token", response.data.token);
    }
    return response.data;
  },

  register: async (user) => {
    const response = await api.post("/auth/register", user);
    return response.data;
  },

  logout: () => {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
  },

  isAuthenticated: () => {
    return !!localStorage.getItem("token");
  },
  
  hasAnyUser: async () => {
    const response = await api.get("/auth/has-user");
    return response.data;
  }
};