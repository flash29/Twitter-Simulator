
let loginClick = document.getElementById("loginsubmit");


let RegClick = document.getElementById("registrationSubmit");

let tweetClick = document.getElementById("tweetButton");

let followClick = document.getElementById("followButton");

let hashClick = document.getElementById("hashButton");

let logOutClick = document.getElementById("logoutButton");

let wsUri = "ws://127.0.0.1:8080/websocket";
let output = document.getElementById("socketFeed");
let output2 = document.getElementById("hashFeed");
let output3 = document.getElementById("status");

let usernameG;

function onTopWrite(message){
    console.log("Here the message is:"+ message);
    output3.removeChild(output3.firstChild);
    var pre = document.createElement("p"); 
    pre.id = "statusContent"; 
    pre.innerHTML = message; 
    output3.appendChild(pre);
}

function writeToGet(message){
    var pre = document.createElement("p"); 
    pre.id = "hashTweets"; 
    pre.innerHTML = message; 
    output2.appendChild(pre);
}

logOutClick.addEventListener("click", ()=>{
    console.log("the user being logged out is"+ usernameG);
    let user ={
        name : usernameG,
    };

    fetch('http://127.0.0.1:8080/logout', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json;charset=utf-8'
        },
        body: JSON.stringify(user)
      }).then(response => {
        console.log(response);
        if(response.ok){
            onTopWrite("Logged Out Successfully");
            location.reload();
        }
        else{
          onTopWrite("Can't logout as user is not logged in");
        }
         return response.json()
        
      }).then(response => {
          console.log(response);
      });
      
});

hashClick.addEventListener("click", ()=>{
    let u2 = document.getElementById("hashContent").value;
    console.log("the hashtag is:" + u2);

    let u3 = u2.replace('#','a');
    

    fetch('http://127.0.0.1:8080/hashtags/'+u3, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json;charset=utf-8'
        },
      }).then(response => {
        console.log(response);
        if(response.ok){
            onTopWrite("Getting hashtag tweets successful");
        }
        else{
          onTopWrite("Can't get hashtags. user not loggedin");
        }
         return response.json()
        
      }).then(response => {
          console.log(response);
          for (let key in response) {
            if (response.hasOwnProperty(key)) {
                console.log(key + " -> " + response[key]);
                writeToGet(response[key]);
            }
        }
      });
      
});

followClick.addEventListener("click", ()=>{
    let u2 = document.getElementById("following").value;
    console.log("the user to be followed is:" + u2);
    console.log("Tweet");
    let user ={
        username : u2,
        username2 : usernameG
    };

    fetch('http://127.0.0.1:8080/following', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json;charset=utf-8'
        },
        body: JSON.stringify(user)
      }).then(response => {
        console.log(response);
        if(response.ok){
            onTopWrite("Followed Successfully");
        }
        else{
          onTopWrite("Can't Follow user not loggedin");
        }
         return response.json()
        
      }).then(response => {
          console.log(response);
      });
      
});

tweetClick.addEventListener("click", ()=>{
    let tweet = document.getElementById("tweetContent").value;
    console.log("the tweet is:" + tweet);
    console.log("Tweet");
    let user ={
        name : usernameG,
        tweetContent : tweet
    };
    if (usernameG){
        fetch('http://127.0.0.1:8080/sendtweets', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json;charset=utf-8'
        },
        body: JSON.stringify(user)
      }).then(response => {
        console.log(response);
        if(response.ok){
            onTopWrite("Tweeted Successfully");
        }
        else{
          onTopWrite("Can't Tweet user not loggedin");
        }
         return response.json()
        
      }).then(response => {
          console.log(response);
      }).catch(err=>{
        onTopWrite("Can't Tweet user not loggedin");
      });
    }
    else{
        onTopWrite("Can't Tweet user not loggedin");
    }

    
      
});

loginClick.addEventListener("click", ()=>{
    let name1 = document.getElementById("name").value;
    let password = document.getElementById("password").value;
    console.log("the name is:" + name1);
    console.log("the name is:" + password);
    console.log("login");
    let user ={
        name : name1,
        password : password
    };

    fetch('http://127.0.0.1:8080/logging', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json;charset=utf-8'
        },
        body: JSON.stringify(user)
      }).then(response => {
        console.log(response);
        if(response.ok){
            usernameG = name1;
            testWebSocket();
            onTopWrite("User logging Successfully");
        }
        else{
          onTopWrite("Can't login User Invalid Credentials");
        }
         return response.json()
        
      }).then(response => {
          console.log(response);
        //  getLatestTweets();
        
      });
      
});

RegClick.addEventListener("click", ()=>{
    let nameReg = document.getElementById("nameReg").value;
    let passwordReg = document.getElementById("passwordReg").value;
    console.log("Registration");
    console.log("the name is:" + nameReg);
    console.log("the name is:" + passwordReg);
    let user ={
        name : nameReg,
        password : passwordReg
    };
    let resp;
    fetch('http://127.0.0.1:8080/userregistration', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json;charset=utf-8'
        },
        body: JSON.stringify(user)
      }).then(response => {
          console.log(response);
          if(response.ok){
              onTopWrite("User Successfully Registered");
          }
          else{
            onTopWrite("Can't Register User");
          }
           return response.json()
          
        }).then(response => {
            console.log(response);
        });
     
   
});


// wsUri = "ws://127.0.0.1:8080/websocket";
function testWebSocket() {
    websocket = new WebSocket(wsUri);
       
    websocket.onopen = function(evt) {
       onOpen(evt)
    };
   
    websocket.onmessage = function(evt) {
       onMessage(evt)
    };
   
    websocket.onerror = function(evt) {
       onError(evt)
    };
 }
   
 function onOpen(evt) {
    let n1 = "name-"+usernameG;
    console.log(n1)
    doSend(n1);
 }
   
 function onMessage(evt) {
     let a = evt.data;
     let b = a.split("-");
     if (b.length>1){
        let divb = "<p id='username'>" + b[0]+ " Tweets "+"\t"+"</p>";
         let divT = "<p id='userTweet'>" + b[1]+"</p>";
         writeToScreen('<div id = "TweetStyle">' + divb + divT +'</div>'); 
     }
     else{
        writeToScreen('<div id = "TweetStyle">' + a +'</div>'); 
     }
     

    
 }

 function onError(evt) {
    writeToScreen('<div id = "TweetStyle">Something is wrong cant reach server </div>' + evt.data);
 }
   
 function doSend(message) {
    console.log("Established the connection to the server" + message);
   // writeToScreen("SENT: " + message); 
    websocket.send(message);
    
 }
   
 function writeToScreen(message) {
    var pre = document.createElement("p"); 
    pre.id = "TweetColors"; 
    pre.innerHTML = message; 
    output.appendChild(pre); 
 }