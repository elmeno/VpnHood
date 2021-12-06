import Vue from 'vue'
import VueRouter from 'vue-router'
import Home from './pages/Home.vue'
import Login from './pages/Login.vue'
import Paywall from './pages/Paywall.vue'

Vue.use(VueRouter)

export default new VueRouter({
  mode: 'history',
  base: process.env.BASE_URL,
  routes: [
    {
      path: '/',
      redirect: '/login'
    },
    {
      path: '/login',
      component: Login
    },
    {
      path: '/paywall',
      component: Paywall
    },
    {
      path: '/home',
      component: Home
    },
    {
      path: '*',
      redirect: '/'
    }
  ]
})
