import Vue from 'vue'
import Vuex from 'vuex'

Vue.use(Vuex)

export default new Vuex.Store({
  state: {
    loading: false,
    loadingText: '加载中...'
  },
  mutations: {
    SET_LOADING(state, { loading, text }) {
      state.loading = loading
      if (text) {
        state.loadingText = text
      }
    }
  },
  actions: {
    updateLoading({ commit }, { loading, text }) {
      commit('SET_LOADING', { loading, text })
    }
  },
  getters: {
    loading: state => state.loading,
    loadingText: state => state.loadingText
  }
})