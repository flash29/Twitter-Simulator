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


// let greetings q =
//   defaultArg (Option.ofChoice (q ^^ "name")) "World" |> sprintf "Hello %s"

// let sample : WebPart = 
//     path "/hello" >=> choose [
//       GET  >=> request (fun r -> OK (greetings r.query))
//       POST >=> request (fun r -> OK (greetings r.form))
//       RequestErrors.NOT_FOUND "Found no handlers" ]

// let mutable newT = ""
// let mutable result = ""

// let infoCheck q =
//     // newT <- Json.mapJsonWith Json.fromJson UTF8.bytes q
//     let a =defaultArg (Option.ofChoice (q ^^ "nameOfuser")) "Not Found" 
//     if a <> "Not Found" then
//         result <- a
//     result


let system = ActorSystem.Create("TwitterServerEngine")

let rand = System.Random()

let mutable socketMaps = Map.empty


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

type SocketProccesser =
    | SendFollowingTweets of string*WebSocket*string
    | OwnTweet of string*WebSocket
    | FollowingUpdates of string*string*WebSocket
    | MentioningUpdates of string*string*WebSocket

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
                    let usernameAndTweet = username + "-" + username_1
                    let byteResponse =
                      usernameAndTweet
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment  
                    let res = socket{
                      do! ws.send Text byteResponse true
                    }
                    Async.StartAsTask res |> ignore
                | MentioningUpdates (username, tweet, ws)->
                    let usernameAndTweet = username + "-" + tweet
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
    printfn "the value of temppass is %s" tempPass.Value 
    printfn "Here is the login Request %A" userinfo
    let mutable response = 200
    if tempPass.Value = userinfo.password then
      logStatus <- logStatus.Add(userinfo.name, "LoggedIN")
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

//here username2 is following username
let followersAddition username username2 = 
    let found = followers.TryFind username
    if found = None then
        let FList = new List<string>()
        FList.Add(username2)
        followers <- followers.Add(username, FList)
    else
        found.Value.Add(username2)

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
            SocketProccesserRef <! SendFollowingTweets(user, soc.Value, tweet)

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

//**********************Socket Connection********************** 


        
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


//******************** Routing ****************************

let app =
  choose
    [ path "/websocket" >=> handShake ws
      GET >=> choose
        [ path "/userinfo" >=> OK "info sent" 
        ]
      POST >=> choose
        [ path "/userregistration" >=> AddingUser
          path "/logging" >=>  LoggingInUser
          path "/logout" >=>  LoggingOutUser
          path "/sendtweets" >=> TweetAccepter
        ] 
    ]

startWebServer defaultConfig app


//let s = "@rj how are you #CL"
// let t = s.Split ' '
// for str in t do
//   printfn "the val is %s" str
//   if str.StartsWith("@") then
//     let attherate = str.[1..]
//     printfn "the string in @ is %s" attherate
//   if str.StartsWith("#") then
//     let attherate = str.[1..]
//     printfn "the string in # is %s" attherate
// let isFound number elem = elem = number 
// let inp = ['a';'b';'c']
// let result = inp |> List.findIndex (isFound 'c')
// printfn "Here the result is : %d" result

// let app : WebPart =
//   choose [
//     GET >=> path "/home" >=> Files.file "index.html"
//     GET >=> Files.browseHome
//     RequestErrors.NOT_FOUND "Page not found." 
//   ]
// let config =
//   { defaultConfig with homeFolder = Some (Path.GetFullPath "./public/") }

// startWebServer config app