<template>
  <div id="sectionWrapper">
    <v-container
      id="sectionWrapperBkgnd"
      fill-height
      fluid
      :class="`px-4 pt-4 px-sm-8 pt-sm-5 state-${connectionState.toLowerCase()}`"
    >
      <v-snackbar top app color="success" v-model="store.newServerAdded">{{$t("newServerAdded")}}</v-snackbar>

      <v-row class="align-self-start">
        <v-dialog
          :value="store.requestedPublicServerProfileId != null"
          width="500"
        >
          <v-card>
            <v-card-title
              class="headline grey lighten-2"
              v-html="$t('publicServerWarningTitle')"
            />
            <v-card-text v-html="$t('publicServerWarning')" class="pt-4" />

            <v-divider></v-divider>
            <v-card-actions>
              <v-spacer></v-spacer>
              <v-btn
                color="primary"
                text
                @click="
                  store.connect(store.requestedPublicServerProfileId, true)
                "
                v-text="$t('ok')"
              />
            </v-card-actions>
          </v-card>
        </v-dialog>

        <!-- top bar -->
        <v-col cols="3" class="pa-0 ma-0">
          <!-- <v-app-bar-nav-icon
            color="white"
            @click.stop="store.navigationDrawer = !store.navigationDrawer"
          ></v-app-bar-nav-icon> -->
        </v-col>
        <v-col cols="6" id="topstate" class="text-center pb-0">
          <span id="stateText">{{ store.connectionStateText("$") }}</span>
        </v-col>
        <!-- <v-col cols="6" id="logo" class="text-center pb-0"> -->
          <!-- <img src="@/assets/images/logo-small.png" :alt="$t('appName')" /> -->
          <!-- <h1 class="">{{ $t("appName") }}</h1> -->
        <!-- </v-col> -->
        <v-col cols="3" class="text-right pa-0 ma-0">
          <!-- Menu -->
          <ClientProfileMenu
            clientProfileId="$"
            color="white"
            :showAddServerItem="true"
            :showDeleteItem="false"
            :showRenameItem="false"
          />
        </v-col>
      </v-row>

      <!-- Speed -->
      <v-row id="speedSection" class="py-0 mt-5">
        <v-col cols="6" class="py-0 my-0 text-right">
          <span class="speedLabel">{{ $t("downloadSpeed") }}:</span>
          <span class="speedValue">{{
            this.formatSpeed(this.store.state.receiveSpeed)
          }}</span>
          <span class="speedUnit">Mbps</span>
        </v-col>
        <v-col cols="6" class="py-0 my-0">
          <span class="speedLabel">{{ $t("uploadSpeed") }}:</span>
          <span class="speedValue">{{
            this.formatSpeed(this.store.state.sendSpeed)
          }}</span>
          <span class="speedUnit">Mbps</span>
        </v-col>
      </v-row>

      <!-- Circles -->
      <v-row
        id="middleSection"
        :class="`state-${connectionState.toLowerCase()} align-self-center`"
      >
        <v-col cols="12" class="ma-0 pa-0" align="center">
          <!-- <div id="circleOuter" class="mb-8"> -->
            <!-- <div id="circle"> -->
              <!-- <div id="circleContent" class="align-center"> -->
                

                <!-- usage -->
                <div
                  v-if="connectionState == 'Connected' && this.bandwidthUsage()"
                >
                  <div id="bandwidthUsage">
                    <span>{{ this.bandwidthUsage().used }} of</span>
                  </div>
                  <div id="bandwithTotal" v-if="connectionState == 'Connected'">
                    <span>{{ this.bandwidthUsage().total }}</span>
                  </div>
                </div>

                <!-- check -->

                <div cols="6" id="logo" class="text-center pb-0">
                  <img 
                    src="@/assets/shield2.png" 
                    :alt="store.connectionStateText('$')" 
                    v-if="
                      connectionState == 'Connected' && !this.bandwidthUsage()
                  "/>
                </div>
                <div cols="6" id="logo" class="text-center pb-0">
                  <img 
                    src="@/assets/shieldPrepare.png" 
                    :alt="store.connectionStateText('$')" 
                    v-if="
                    connectionState === 'Connecting' ||
                    connectionState === 'Disconnecting' ||
                    connectionState === 'Diagnosing'
                  "/>
                </div>
                <div cols="6" id="logo" class="text-center pb-0">
                  <img 
                    src="@/assets/shieldOffline2.png" 
                    :alt="store.connectionStateText('$')" 
                    v-if="connectionState == 'None'"
                  />
                </div>
              <!-- </div> -->
            <!-- </div> -->
          <!-- </div> -->

          <!-- Connect Button -->
          <v-btn
            v-if="connectionState == 'None'"
            id="connectButton"
            class="main-button"
            @click="store.connect('$')"
          >
            {{ $t("connect") }}
          </v-btn>

          <!-- Diconnect Button -->
          <v-btn
            v-if="
              connectionState == 'Connecting' ||
              connectionState == 'Connected' ||
              connectionState == 'Diagnosing'
            "
            id="disconnectButton"
            class="main-button"
            @click="store.disconnect()"
          >
            <span>{{ $t("disconnect") }}</span>
          </v-btn>

          <!-- Diconnecting -->
          <v-btn
            v-if="connectionState == 'Disconnecting'"
            id="disconnectingButton"
            class="main-button"
            style="pointer-events: none"
          >
            <span>{{ $t("disconnecting") }}</span>
          </v-btn>
        </v-col>
      </v-row>

      <!-- Config -->
      <v-row id="configSection" class="align-self-end">
        <!-- *** appFilter *** -->
        <v-col
          cols="12"
          class="py-1"
          v-if="
            store.features.isExcludeApplicationsSupported ||
            store.features.isIncludeApplicationsSupported
          "
        >
          <v-icon class="config-icon" @click="showAppFilterSheet()"
            >apps</v-icon
          >
          <span class="config-label" @click="showAppFilterSheet()">{{
            $t("appFilterStatus_title")
          }}</span>
          <v-icon class="config-arrow" flat @click="showAppFilterSheet()"
            >keyboard_arrow_right</v-icon
          >
          <span class="config" @click="showAppFilterSheet()">
            {{ this.appFilterStatus }}</span
          >
        </v-col>

        <!-- *** Protocol *** -->
        <!-- <v-col cols="12" class="py-1">
          <v-icon class="config-icon" @click="showProtocolSheet()"
            >settings_ethernet</v-icon
          >
          <span class="config-label" @click="showProtocolSheet()">{{
            $t("protocol_title")
          }}</span>
          <v-icon class="config-arrow" flat @click="showProtocolSheet()"
            >keyboard_arrow_right</v-icon
          >
          <span class="config" @click="showProtocolSheet()">
            {{ protocolStatus }}</span
          >
        </v-col> -->

        <!-- *** server *** -->
        <v-col cols="12" class="py-1 selectedServer" @click="showServersSheet()">
          <!-- <v-icon class="config-icon" @click="showServersSheet()">dns</v-icon>
          <span class="config-label" @click="showServersSheet()">{{
            $t("selectedServer")
          }}</span> -->
          <country-flag :country='store.clientProfile.countryCode("$")' size='normal'/>
          <span class="config" @click="showServersSheet()">
            {{ store.clientProfile.name("$") }}</span
          >
          <v-icon class="config-arrow" flat 
            >keyboard_arrow_down</v-icon
          >
        </v-col>
      </v-row>
    </v-container>
  </div>
  <!-- rootContaier -->
</template>

<style>
@import "../assets/styles/custom.css";

.selectedServer {
  text-align: center;
  display: flex;
  align-items: center;
  justify-content: center;
}
.selectedServer .config {
  font-size: 20px;
  margin: 0 10px;
}
.selectedServer .config-arrow {
    font-size: 30px;
}
.v-input--checkbox .v-label {
  font-size: 12px;
}
</style>

<script>
import ClientProfileMenu from "../components/ClientProfileMenu";

export default {
  name: "HomePage",
  components: {
    ClientProfileMenu
  },
  created() {
    this.store.setTitle(this.$t("home"));
    this.monitorId = setInterval(() => {
      if (!document.hidden)
        this.store.updateState();
    }, 1000);

  },
  beforeDestroy() {
    clearInterval(this.monitorId);
    this.monitorId = 0;
  },
  data: () => ({
  }),
  computed: {
    connectionState() { return this.store.connectionState("$"); },
    appFilterStatus() {
      if (this.store.userSettings.appFiltersMode == 'Exclude') return this.$t("appFilterStatus_exclude", { x: this.store.userSettings.appFilters.length });
      if (this.store.userSettings.appFiltersMode == 'Include') return this.$t("appFilterStatus_include", { x: this.store.userSettings.appFilters.length });
      return this.$t("appFilterStatus_all");
    },
    protocolStatus() {
      return (this.store.userSettings.useUdpChannel) ? this.$t('protocol_udpOn') : this.$t('protocol_udpOff');
    }
  },
  methods: {
    async remove(clientProfileId) {
      clientProfileId = this.store.clientProfile.updateId(clientProfileId);
      const res = await this.$confirm(this.$t("confirmRemoveServer"), { title: this.$t("warning") })
      if (res) {
        await this.store.invoke("removeClientProfile", { clientProfileId });
        this.store.loadApp();
      }
    },

    editClientProfile(clientProfileId) {
      window.gtag('event', "editprofile");
      clientProfileId = this.store.clientProfile.updateId(clientProfileId);
      this.$router.push({ path: this.$route.path, query: { ... this.$route.query, editprofile: clientProfileId } })
    },

    showAddServerSheet() {
      window.gtag('event', "addServerButton");
      this.$router.push({ path: this.$route.path, query: { ... this.$route.query, addserver: '1' } })
    },

    showServersSheet() {
      window.gtag('event', "changeServer");
      this.$router.push({ path: this.$route.path, query: { ... this.$route.query, servers: '1' } })
    },

    showProtocolSheet() {
      window.gtag('event', "changeProtocol");
      this.$router.push({ path: this.$route.path, query: { ... this.$route.query, protocol: '1' } })
    },

    showAppFilterSheet() {
      window.gtag('event', "changeAppFilter");
      this.$router.push({ path: this.$route.path, query: { ... this.$route.query, appFilter: '1' } })
    },

    bandwidthUsage() {
      if (!this.store.state || !this.store.state.sessionStatus || !this.store.state.sessionStatus.accessUsage)
        return null;
      let accessUsage = this.store.state.sessionStatus.accessUsage;
      if (accessUsage.maxTrafficByteCount == 0)
        return null;

      let mb = 1000000;
      let gb = 1000 * mb;

      let ret = { used: accessUsage.sentByteCount + accessUsage.receivedByteCount, total: accessUsage.maxTrafficByteCount };
      // let ret = { used: 100 * mb, total: 2000 * mb };

      if (ret.total > 1000 * mb) {
        ret.used = (ret.used / gb).toFixed(0) + "GB";
        ret.total = (ret.total / gb) + "GB";
      }
      else {
        ret.used = (ret.used / mb).toFixed(0) + "MB";
        ret.total = (ret.total / mb).toFixed(0) + "MB";
      }

      return ret;
    },

    formatSpeed(speed) {
      return (speed * 10 / 1000000).toFixed(2);
    },
  }
}
</script>
