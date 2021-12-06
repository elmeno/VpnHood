<template>
  <div id="appl">
    <div class="login-page">
      <div class="wallpaper-register"></div>
      <transition name="fade">
        <div v-if="!registerActive" class="wallpaper-login"></div>
      </transition>

      <div class="container">
        <div class="row">
          <div class="col-lg-4 col-md-6 col-sm-8 mx-auto">
            <div
              v-if="!registerActive"
              class="card login"
              v-bind:class="{ error: emptyFields }"
            >
              <h1>Sign In</h1>
              <p class="alert" v-show="errorMessage">{{ errorMessage }}</p>
              <form class="form-group">
                <input
                  v-model="emailLogin"
                  type="email"
                  class="form-control"
                  placeholder="Email"
                  required
                />
                <input
                  v-model="passwordLogin"
                  type="password"
                  class="form-control"
                  placeholder="Password"
                  required
                />
                <input type="submit" class="btn btn-primary" @click="doLogin" />
                <p>
                  Don't have an account?
                  <a
                    href="#"
                    @click="
                      (registerActive = !registerActive), (emptyFields = false)
                    "
                    >Sign up here</a
                  >
                </p>
                <p>
                  <a href="#" @click="doForgot">Forgot your password?</a>
                </p>
              </form>
            </div>

            <div
              v-else
              class="card register"
              v-bind:class="{ error: emptyFields }"
            >
              <h1>Sign Up</h1>
              <form class="form-group">
                <input
                  v-model="emailReg"
                  type="email"
                  class="form-control"
                  placeholder="Email"
                  required
                />
                <input
                  v-model="passwordReg"
                  type="password"
                  class="form-control"
                  placeholder="Password"
                  required
                />
                <input
                  v-model="confirmReg"
                  type="password"
                  class="form-control"
                  placeholder="Confirm Password"
                  required
                />
                <input
                  type="submit"
                  class="btn btn-primary"
                  @click="doRegister"
                />
                <p>
                  Already have an account?
                  <a
                    href="#"
                    @click="
                      (registerActive = !registerActive), (emptyFields = false)
                    "
                    >Sign in here</a
                  >
                </p>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
@import "../assets/styles/bootstrap.min.css";
p {
  line-height: 1rem;
}

.card {
  padding: 20px;
}

.form-group input {
  margin-bottom: 20px;
}

.login-page {
  align-items: center;
  display: flex;
  height: 100vh;
}

.login-page .wallpaper-login {
  background: url(../assets/images/night-913046_1280.jpg) no-repeat center
    center;
  background-size: cover;
  height: 100%;
  position: absolute;
  width: 100%;
}

.login-page .fade-enter-active,
.login-page .fade-leave-active {
  transition: opacity 0.5s;
}
.login-page .fade-enter,
.login-page .fade-leave-to {
  opacity: 0;
}

.login-page .wallpaper-register {
  background: url(../assets/images/batemans-tower-6210374_1280.jpg) no-repeat
    center center;
  background-size: cover;
  height: 100%;
  position: absolute;
  width: 100%;
  /* z-index: -1; */
}

.login-page h1 {
  margin-bottom: 1.5rem;
}
.error {
  animation-name: errorShake;
  animation-duration: 0.3s;
}

@keyframes errorShake {
  0% {
    transform: translateX(-25px);
  }
  25% {
    transform: translateX(25px);
  }
  50% {
    transform: translateX(-25px);
  }
  75% {
    transform: translateX(25px);
  }
  100% {
    transform: translateX(0);
  }
}
</style>

<script>
export default {
  // el: "#appl",
  created() {
    const isLoggedIn = this.store.isLoggedIn()
    if (isLoggedIn) this.$router.push("/home")
  },
  data: () => ({
    errorMessage: "",
    registerActive: false,
    emailLogin: "",
    passwordLogin: "",
    emailReg: "",
    passwordReg: "",
    confirmReg: "",
    emptyFields: false,
  }),

  methods: {
    doLogin(event) {
      event.preventDefault();
      const run = async () => {
        if (this.emailLogin === "" || this.passwordLogin === "") {
          this.emptyFields = true;
        } else {
          const result = await this.store.login(
            this.emailLogin,
            this.passwordLogin
          );

          if (!result.success) {
            this.emptyFields = true;
            this.errorMessage = result.errorMessage || "Wrong password!";
          } else {
            this.errorMessage = "";
            this.emptyFields = false;
            window.gtag("event", "login");
            this.$router.push("/home");
          }
        }
      };
      run();
    },

    doRegister(event) {
      event.preventDefault();
      const run = async () => {
        if (
          this.emailReg === "" ||
          this.passwordReg === "" ||
          this.confirmReg === "" ||
          this.passwordReg !== this.confirmReg
        ) {
          this.emptyFields = true;
        } else {
          const result = await this.store.register(
            this.emailReg,
            this.passwordReg
          );

          if (!result.success) {
            this.emptyFields = true;
            this.errorMessage = result.errorMessage || "Unknown Error!";
          } else {
            this.errorMessage = "";
            this.emptyFields = false;
            window.gtag("event", "register");
            this.$router.push("/home");
          }
        }
      };
      run();
    },

    async doForgot(event) {
      event.preventDefault();
      this.passwordReg === "";
      this.store.forgot(this.emailLogin);
      window.gtag("event", "forgot");
    },
  },
};
</script>
