<template>
  <div id="appl">
    <div class="page">
      <div class="premiumWrapper col-lg-4 col-md-6 col-sm-8 mx-auto">
        <div class="premiumImage">
          <img
            src="../assets/images/undraw_Security_on_ff2u.svg"
            alt="premium"
          />
        </div>

        <div class="premiumTitle">YETI VPN Premium</div>

        <div class="subtitles">
          <div class="subtitle">Blazing fast connection speed</div>
          <div class="subtitle">Military-grade encryption</div>
          <div class="subtitle">Unlock all virtual locations</div>
          <div class="subtitle">Tech support</div>
          <div class="subtitle">All upcoming features</div>
        </div>

        <form class="checkoutform">
          <button
            type="submit"
            class="checkout btn btn-primary"
            @click="doCheckout"
          >
            UPGRADE TO PREMIUM
          </button>
        </form>
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

.page {
  align-items: center;
  display: flex;
  height: 100vh;
  font-family: sans-serif, "Apple Color Emoji", "Segoe UI Emoji",
    "Segoe UI Symbol", "Noto Color Emoji" !important;
}

.premiumWrapper {
  display: flex;
  flex-direction: column;
  justify-content: space-evenly;
  height: 100%;
}

.login-page .wallpaper-login {
  background: url(../assets/images/night-913046_1280.jpg) no-repeat center
    center;
  background-size: cover;
  height: 100%;
  position: absolute;
  width: 100%;
}

.login-page h1 {
  margin-bottom: 1.5rem;
}

.premiumImage img {
  width: 100%;
  height: 33vh;
}

.premiumTitle {
  text-align: center;
  font-size: 1.5em;
}
.subtitles {
  text-align: center;
  font-size: 1.1em;
}
.checkoutform {
  justify-content: center;
  display: flex;
  margin-top: 1rem;
}
.checkout {
  background-color: #1075bb;
  border-radius: 50px;
  padding: 1em 2em;
  font-size: 1.1em;
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
  async created() {

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
    async doCheckout(event) {
      event.preventDefault();

      this.checkInterval = setInterval(this.checkToken, 2000);

      this.store.purchase();
      window.gtag("event", "doCheckout");
    },
    
    async checkToken() {
      if(this.store.isSubscribed){
        clearInterval(this.checkInterval);
        this.$router.push("/home");
        return
      }
      const account = await this.store.checkToken();
      // console.log("account", account);
      const isSubscribed =
        account &&
        account.paymentInfo &&
        account.paymentInfo.status === "ACTIVE";
      if (isSubscribed) {
        this.store.updateSubStatus(isSubscribed)
        clearInterval(this.checkInterval);
        this.$router.push("/home");
      }
    },
  },
};
</script>
