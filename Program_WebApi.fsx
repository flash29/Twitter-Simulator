#!/usr/bin/env fsharpi
// #r "./packages/Suave/lib/net40/Suave.dll"
#r "nuget: Suave, 2.6.1"
#r "nuget: FSharp.Json, 0.4.1"
#r "nuget: Akka, 1.4.28"
#r "nuget: Akka.FSharp, 1.4.28"
#r "nuget: Akka.Remote, 1.4.28"
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Utils.Collections
open System.IO
open Suave.Json
open System.Text.Json
open FSharp.Json
open Suave.RequestErrors
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Threading
open System
open System.Data
open System.Collections.Generic
open System.Text
open System.Diagnostics
open Suave.Writers

let system = ActorSystem.Create("TwitterServerEngine")


let mutable socketMaps = Map.empty

let setCORSHeaders =
    setHeader  "Access-Control-Allow-Origin" "*"
    >=> setHeader "Access-Control-Allow-Headers" "content-type"

let allow_cors : WebPart =
    choose [
        OPTIONS >=>
            fun context ->
                context |> (
                    setCORSHeaders
                    >=> OK "CORS approved" )
    ]
// ************** Registring users *****************

//list of all the users along with their passwords 
//Key: username value:password
let mutable users = Map.empty

type UserReg = {
    name : string
    password : string
}
let registeringUsers (userinfo :UserReg) =
    printfn "Here is the registrstion Request %A" userinfo
    users <- users.Add(userinfo.name, userinfo.password)
    printfn "here is the list of users %A" users
    userinfo


let AddingUser =
    request (
        fun r ->
        r.rawForm
        |>System.Text.Encoding.UTF8.GetString
        |>Json.deserialize<UserReg>
        |>registeringUsers
        |>Json.serialize
        |>OK
        )
        >=> setMimeType "application/json"
        >=> setCORSHeaders

type SocketProccesser =
    | SendFollowingTweets of string*WebSocket*string
    | OwnTweet of string*WebSocket
    | FollowingUpdates of string*string*WebSocket
    | MentioningUpdates of string*string*WebSocket
    | MentioningUpdatesLogIn of WebSocket*string

let SocketProccesser (mailbox: Actor<_>) =
    
    // let mutable hashedValue
    let rec loop() = actor {
                let! message = mailbox.Receive()
                match message with
                | SendFollowingTweets (name, ws,tweet) -> 
                    let usernameAndTweet = name + "-" + tweet
                    let byteResponse =
                      usernameAndTweet
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment  
                    let res = socket{
                      do! ws.send Text byteResponse true
                    }
                    Async.StartAsTask res |> ignore
                | OwnTweet (tweet, ws) ->
                    let byteResponse =
                      tweet
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment  
                    let res = socket{
                      do! ws.send Text byteResponse true
                    }
                    Async.StartAsTask res |> ignore
                | FollowingUpdates (username, username_1, ws) ->
                    let usernameAndTweet = username_1 + " Started Following you" 
                    let byteResponse =
                      usernameAndTweet
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment  
                    let res = socket{
                      do! ws.send Text byteResponse true
                    }
                    Async.StartAsTask res |> ignore
                | MentioningUpdates (username, tweet, ws)->
                    let usernameAndTweet =  username + "Mentioned you in a tweet : " + tweet
                    let byteResponse =
                      usernameAndTweet
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment  
                    let res = socket{
                      do! ws.send Text byteResponse true
                    }
                    Async.StartAsTask res |> ignore
                | MentioningUpdatesLogIn (ws, tweet) ->
                    let usernameAndTweet =    "  you were mentioned in a tweet: " + tweet
                    let byteResponse =
                      usernameAndTweet
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment  
                    let res = socket{
                      do! ws.send Text byteResponse true
                    }
                    Async.StartAsTask res |> ignore

                return! loop()
            }
    loop()

let SocketProccesserRef = spawn system "SocketCreater" SocketProccesser




// ************ LOGGING IN AND LOGGING OUT ********************

// keeping track of users who are logging in and who are logging out
let mutable logStatus = Map.empty

let UpdateLogIn (userinfo: UserReg)=
    let tempPass = users.TryFind userinfo.name
  //  printfn "the value of temppass is %s" tempPass.Value 
  //  printfn "Here is the login Request %A" userinfo
    let mutable response = 200
    if tempPass<>None && tempPass.Value = userinfo.password then
      logStatus <- logStatus.Add(userinfo.name, "LoggedIN")
     // getTweetsToLoggedInUser userinfo.name
    else
      response <- 404
    printfn "here is the list of users %A" logStatus
    response, userinfo

type UserLogOut = {
    name : string
}

let UpdateLogOut (userinfo: UserLogOut)=
    let tempPass = logStatus.TryFind userinfo.name
    printfn "the value of tempStatus is %s" tempPass.Value 
    printfn "Here is the login Request %A" userinfo
    let mutable response = 200
    if tempPass.Value = "LoggedIN" then
      logStatus <- logStatus.Add(userinfo.name, "LoggedOUT")
    else
      response <- 404
    printfn "here is the list of users %A" logStatus
    response, userinfo
    

let LoggingInUser =
    request (
        fun r ->
        let resp, userin =r.rawForm
                          |>System.Text.Encoding.UTF8.GetString
                          |>Json.deserialize<UserReg>
                          |>UpdateLogIn
                  
        if resp = 200 then
            userin
              |>Json.serialize
              |>OK
        else 
           userin
              |>Json.serialize
              |>BAD_REQUEST 
        )  
        >=> setMimeType "application/json"
        >=> setCORSHeaders    

let LoggingOutUser =
    request (
        fun r ->
        let resp, userin =r.rawForm
                          |>System.Text.Encoding.UTF8.GetString
                          |>Json.deserialize<UserLogOut>
                          |>UpdateLogOut
                  
        if resp = 200 then
            userin
              |>Json.serialize
              |>OK
        else 
           userin
              |>Json.serialize
              |>BAD_REQUEST 
        )  
        >=> setMimeType "application/json"
        >=> setCORSHeaders       

//*********************** Tweet Accepting and parsing *************************************

type tweets = {
    name : string
    tweetContent : string
}

// All these maps are of the type Key:username and value:tweet
//For storing tweets made by a specific user
let mutable userTweets = Map.empty
//For storing mentions made of a specific user
let mutable mentions = Map.empty
//For storing hashtags of a specific topic
let mutable hashTags = Map.empty

//This map followers is of the form key:username value is alist of al the users who follow the user
let mutable followers = Map.empty

let mutable following = Map.empty

//************************ Adding Followers ***********************************

//here username is being followed by username2
type following = {
    username : string
    username2 : string
}
//here username2 is following username

let followersAdditionReverse (info:following) = 
    let found = following.TryFind info.username2
    if found = None then
        let FList = new List<string>()
        FList.Add(info.username)
        following <- following.Add(info.username2, FList)
    else
        found.Value.Add(info.username)

let followersAddition (info:following) = 
    let found = followers.TryFind info.username
    let mutable response = 200
    followersAdditionReverse info
    let soc = socketMaps.TryFind info.username
    let soc2 = socketMaps.TryFind info.username2
    if soc <> None then
     SocketProccesserRef <! FollowingUpdates (info.username, info.username2, soc.Value)
     let flist = userTweets.TryFind info.username
     if flist<>None then
      for twee in flist.Value do
        SocketProccesserRef <! SendFollowingTweets (info.username, soc2.Value, twee)
    else 
      response <- 400
    if found = None then
        let FList = new List<string>()
        FList.Add(info.username2)
        followers <- followers.Add(info.username, FList)
    else
        found.Value.Add(info.username2)
    response, info
    

//********************** Tweet Additions ***************************************
let addSpecificUserTweets username tweet = 
    let found = userTweets.TryFind username
    if found = None then
        let FList = new List<string>()
        FList.Add(tweet)
        userTweets <- userTweets.Add(username, FList)
    else
        found.Value.Add(tweet)

let addMentions mentionedUser tweet =
    let found = mentions.TryFind mentionedUser
    if found = None then
        let FList = new List<string>()
        FList.Add(tweet)
        mentions <- mentions.Add(mentionedUser, FList)
    else
        found.Value.Add(tweet)

let addHashtag hashtag tweet = 
    let found = hashTags.TryFind hashtag
    if found = None then
        let FList = new List<string>()
        FList.Add(tweet)
        hashTags <- hashTags.Add(hashtag, FList)
    else
        found.Value.Add(tweet)
    printfn "the map of hashtags is %A" hashTags

let parsingTweets (name:string)(tweet:string) =
    let t = tweet.Split ' '
    for str in t do
     // printfn "the val is %s" str
      if str.StartsWith("@") then
        let attherate = str.[1..]
        addMentions attherate tweet
        let tempsoc = socketMaps.TryFind attherate
        if tempsoc<>None then
          SocketProccesserRef <! MentioningUpdates(name, tweet, tempsoc.Value)
          printfn "the string in @ is %s" attherate
      if str.StartsWith("#") then
        let attherate = str.[1..]
        addHashtag attherate tweet
        printfn "the string in # is %s" attherate



let SendingTweetsToFollowers username tweet = 
    let found = followers.TryFind username
    if found <> None then
        for user in found.Value do
          let tempLog = logStatus.TryFind user
          if tempLog.Value = "LoggedIN" then
            let soc = socketMaps.TryFind user
            SocketProccesserRef <! SendFollowingTweets(username, soc.Value, tweet)

type TweetProccesser =
    | SpecificTweets of string*string
    | ParsingWorker of string*string
    | SendTweetsToFollowers of string*string

let TweetProccesser (mailbox: Actor<_>) =
    
    // let mutable hashedValue
    let rec loop() = actor {
                let! message = mailbox.Receive()
                match message with
                | SpecificTweets (name, tweet) ->   
                    addSpecificUserTweets name tweet
                | ParsingWorker (name, tweet) ->
                    parsingTweets name tweet
                | SendTweetsToFollowers (username, tweet) ->
                    SendingTweetsToFollowers username tweet
                return! loop()
            }
    loop()

let TweetProccesserRef = spawn system "Creater" TweetProccesser

let TweetAddition (userinfo: tweets)= 
    let mutable response = 200
    let tempLog = logStatus.TryFind userinfo.name
    if tempLog.Value = "LoggedIN" then
      let soc = socketMaps.TryFind userinfo.name
      if soc <> None then
        SocketProccesserRef <! OwnTweet (userinfo.tweetContent, soc.Value)
      TweetProccesserRef <! SpecificTweets (userinfo.name, userinfo.tweetContent)
      TweetProccesserRef <! ParsingWorker (userinfo.name, userinfo.tweetContent)
      TweetProccesserRef <! SendTweetsToFollowers(userinfo.name, userinfo.tweetContent)
    else
      response <- 404
    response, userinfo


let TweetAccepter =
    request (
        fun r ->
        let resp, userin =r.rawForm
                          |>System.Text.Encoding.UTF8.GetString
                          |>Json.deserialize<tweets>
                          |>TweetAddition
                  
        if resp = 200 then
            userin
              |>Json.serialize
              |>OK
        else 
           userin
              |>Json.serialize
              |>BAD_REQUEST 
        ) 
        >=> setMimeType "application/json"
        >=> setCORSHeaders

//******************* Followers Accepter and proccessors ********************************

let FollowerAccepter =
    request (
        fun r ->
        let resp, userin =r.rawForm
                          |>System.Text.Encoding.UTF8.GetString
                          |>Json.deserialize<following>
                          |>followersAddition
                  
        if resp = 200 then
            userin
              |>Json.serialize
              |>OK
        else 
           userin
              |>Json.serialize
              |>BAD_REQUEST 
        ) 
        >=> setMimeType "application/json"
        >=> setCORSHeaders

//******************************* HashTags *****************************
type HashTagsType ={
  hash :string
}

let HashTagGetter hash = 
    printfn "the request for retrieving hashtags is : %s " hash
    let mutable response = 200
    let tempHash = hash.[1..]
    let tempLog = hashTags.TryFind tempHash
    let mutable FList = new List<string>()
    let mutable tweetMap = Map.empty
    if tempLog <> None then
      let mutable counter = 0
      FList <- tempLog.Value
      for i in tempLog.Value do
        counter <- counter + 1
        tweetMap <-tweetMap.Add((string)counter,i)
      printfn "the value of FList in hashtags is: %A " FList
      printfn "After iterations this is tweetMap: %A " tweetMap
    else
      response <- 404
    tweetMap

let HashTagRequester hash=
        HashTagGetter hash 
        |>Json.serialize
        |>OK        
        // if resp = 200 then
        //     userin
        //       |>Json.serialize
        //       |>OK
        // else 
        //    userin
        //       |>Json.serialize
        //       |>BAD_REQUEST 
        >=> setMimeType "application/json"
        >=> setCORSHeaders 
//*********************** After Login **************************

let getTweetsToLoggedInUser username =
  let FList = following.TryFind username
  let soc = socketMaps.TryFind username
  if FList <> None then
    let temp = FList.Value
    for user in temp do
      let uTweets = userTweets.TryFind user
      if uTweets<>None then
        for twee in uTweets.Value do
          SocketProccesserRef <! SendFollowingTweets (user, soc.Value, twee )
  let nFlist = mentions.TryFind username
  if nFlist <> None then
    let t2 = nFlist.Value
    for twee in t2 do
      SocketProccesserRef <! MentioningUpdatesLogIn ( soc.Value, twee )


let getTweetsAfterLogIN username=
        getTweetsToLoggedInUser username 
        |>Json.serialize
        |>OK        
        >=> setMimeType "application/json"
        >=> setCORSHeaders 


//********************** Socket Connection **********************
        
let ws (webSocket : WebSocket) (context: HttpContext) =
    socket {
      let mutable loop = true

      while loop do
          let! msg = webSocket.read()

          match msg with
          | (Text, data, true) ->
              let str = UTF8.toString data
              if str.StartsWith("name-") then
                let tempstr = str.Split "-"
                printfn "username found is :%s" tempstr.[1]
                socketMaps <- socketMaps.Add(tempstr.[1], webSocket)
                getTweetsToLoggedInUser tempstr.[1]
                printfn "here is the list of users %A" socketMaps
              else
                let response = sprintf "response to %s" str
                printfn "printing the websocket %A" webSocket 
             //   printfn "printing the context %A" context
                let byteResponse =
                    response
                    |> System.Text.Encoding.ASCII.GetBytes
                    |> ByteSegment
                do! webSocket.send Text byteResponse true

          | (Close, _, _) ->
              let emptyResponse = [||] |> ByteSegment
              do! webSocket.send Close emptyResponse true
              loop <- false

          | _ -> ()
    }
//********************** End of Socket Connection ********************** 

//******************** Routing ****************************

let app =
  choose
    [ 
      path "/websocket" >=> handShake ws
      allow_cors
      GET >=> choose
        [ 
          pathScan "/hashtags/%s" (fun hashtag -> (HashTagRequester hashtag) ) 
        ]
      POST >=> choose
        [ path "/userregistration" >=> AddingUser
          path "/logging" >=>  LoggingInUser
          path "/logout" >=>  LoggingOutUser
          path "/sendtweets" >=> TweetAccepter
          path "/following" >=> FollowerAccepter
        ] 
    ]

//******************** End of Routing ****************************

//******************** Starting the server ****************************

startWebServer defaultConfig app

//******************** End of Code ****************************
