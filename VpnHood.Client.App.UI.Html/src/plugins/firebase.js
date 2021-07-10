import firebase from 'firebase/app'
import 'firebase/storage'

const firebaseConfig = {
  apiKey: 'AIzaSyAJLLevesi1uwo52RVXFC7w8waW80QoNCg',
  authDomain: 'yetivpn-pc.firebaseapp.com',
  projectId: 'yetivpn-pc',
  storageBucket: 'yetivpn-pc.appspot.com',
  messagingSenderId: '950157896343',
  appId: '1:950157896343:web:f25dac83f8cb7a41ae97d2',
  measurementId: 'G-08WLL1FYJ1'
}

// Initialize Firebase
firebase.initializeApp(firebaseConfig)
// firebase.analytics()

// firebase.auth().signInAnonymously()
//   .then(() => {
//     // Signed in..
//     console.log("OK");
//   })
//   .catch((error) => {
//     var errorCode = error.code;
//     var errorMessage = error.message;
//     console.log(errorCode);
//     console.log(errorMessage);
//     // ...
//   })
