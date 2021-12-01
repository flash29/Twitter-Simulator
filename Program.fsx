#r "nuget: Akka, 1.4.28"
#r "nuget: Akka.FSharp, 1.4.28"
#r "nuget: Akka.Remote, 1.4.28"
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Threading
open System
open System.Data
open System.Collections.Generic

let Configuration = ConfigurationFactory.ParseString(
    @"akka{
        actor{
            provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            debug : {
                receive : on
                autoreceive : on
                lifecycle : on
                event-stream : on
                unhandled : on
            }
        }
        remote {
            helios.tcp{
                port = 8777
                hostname = localhost
            }
        }
    }"
)

let system = ActorSystem.Create("TwitterServer", Configuration)
let rand = System.Random()

let mutable users = Map.empty
let mutable logStatus = Map.empty
//list of all the tweets
let mutable tweets = []
//list of all the tweets mapped to a specific user
let mutable userTweet = Map.empty
let mutable followers = Map.empty
let mutable mentions = Map.empty
let mutable hashTags = Map.empty
let mutable subscribedTweets = Map.empty
let id = 1
let nodes = 10


let getUserId username = 
    let e = users.TryFind username
    e

let addFollowers username =
    let mutable numOfFollowers = rand.Next()
    let FList = new List<string>()
    numOfFollowers <- numOfFollowers % users.Count
    let userID = getUserId username
   // printfn "Here from addFollowers numof follow: %d" numOfFollowers
    for i in 0..numOfFollowers do
        let t = rand.Next() % users.Count
        let u =users.TryFind ("User"+(string t))
        if u <> userID then
            FList.Add("User"+(string t))
    followers <- followers.Add(username, FList)
  //  printfn "The list of followers to be added are %A" FList
    

let retweets username tweet =
  //  let mutable FList = new List<string>()
    let mutable (FList: List<string>) = userTweet.GetValueOrDefault username
  //  FList <- FList.Add(tweet)
    FList.Add(tweet)
    userTweet <- userTweet.Add(username, FList)

let getMentions mention =
    let found, value =mentions.TryGetValue mention
    value

let getHashTags hashtag =
    let found, value =hashTags.TryGetValue hashtag
    value

let registeringUsers username =
    users <- users.Add(username,rand.Next()|>string)



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

let addSpecificUserTweets username tweet = 
    let found = userTweet.TryFind username
    if found = None then
        let FList = new List<string>()
        FList.Add(tweet)
        userTweet <- userTweet.Add(username, FList)
    else
        found.Value.Add(tweet)


let addToSubscribedUsers username tweet =
    let found = followers.TryFind username
    for i in 1..found.Value.Count do
        let temp = subscribedTweets.TryFind (found.Value.Item(i-1))
        let FList = new List<string>()
        if temp = None then
            FList.Add(tweet)
            subscribedTweets <- subscribedTweets.Add(username, FList)
        else
            temp.Value.Add(tweet)
    

let AddTweet username tweet =
    tweets <- tweets @ [tweet]
    let res = tweet|>string
    let t = (res).Split '/'
    if t.Length = 3 then 
        addMentions t.[0].[1..] tweet |> ignore
        addHashtag t.[2] tweet |> ignore
    addSpecificUserTweets username tweet |> ignore
  //  addToSubscribedUsers username tweet

let getSubscribedTweets username = 
   let found = subscribedTweets.TryFind username
   found



type CreaterHandler =
    | RegisterUsers of string
    | FollowersInit of string

let CreaterHandler (mailbox: Actor<_>) =
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with 
        | RegisterUsers (username) ->
            registeringUsers username
        | FollowersInit (username) ->
            addFollowers username |> ignore
        return! loop()
    }
    loop()

let CreaterHandlerRef = spawn system "Creater" CreaterHandler

type OperationsHandler =
    // | Login of string
    // | Logout of string
    | Tweet of string*string
    | Retweet of string
    | MentionRetrieve of string
    | HashtagRetrieve of string
    | SubscribedRetrieve of string

let OperationsHandler (mailbox: Actor<_>) =
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with 
        | Tweet (username, tweet) ->
            AddTweet username tweet |> ignore
        | Retweet (username) ->
            let tweet_no = rand.Next() % tweets.Length
            retweets username (tweets.Item(tweet_no))
        | MentionRetrieve (mention) ->
            getMentions mention |> ignore
        | HashtagRetrieve (hashtag) ->
            getHashTags hashtag |> ignore
        | SubscribedRetrieve (username) ->
            getSubscribedTweets username |> ignore
        return! loop()
    }
    loop()

let OperationsHandlerRef = spawn system "Operation" OperationsHandler


let startSystem = 
                spawn system "Handler"
                <| fun mailbox ->
                    let rec loop() = 
                        actor { 
                            let! msg = mailbox.Receive()
                            let mutable n = 0
                           // let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                            let response = msg|>string
                            let input = (response).Split '-'
                            printfn "Processing : %s" response
                            if input.[0].CompareTo("Register") = 0 then
                                CreaterHandlerRef <! RegisterUsers (input.[1])
                                logStatus <- logStatus.Add(input.[1], "LoggedIN")
                            else if input.[0].CompareTo("Followers")=0 then
                                CreaterHandlerRef <! FollowersInit(input.[1])
                            else if input.[0].CompareTo("Tweets") = 0 then
                              //  printfn "from the inside %A" input
                                OperationsHandlerRef <! Tweet (input.[2], input.[1])
                            else if input.[0].CompareTo("Retweet") = 0 then
                                OperationsHandlerRef <! Retweet input.[1]
                            else if input.[0].CompareTo("GetMentions") = 0 then
                                OperationsHandlerRef <! MentionRetrieve input.[1]
                            else if input.[0].CompareTo("GetHashTags") = 0 then
                                OperationsHandlerRef <! HashtagRetrieve input.[1]
                            else if input.[0].CompareTo("GetSubscribedTweets") = 0 then
                                OperationsHandlerRef <! SubscribedRetrieve input.[1]
                            
                            // printfn "This is the mention map %A" mentions
                            // printfn "This is the hashtags map %A" hashTags
                            // printfn "This is the usertweets map %A" userTweet
                            // printfn "This is the followers of a particular user map %A" followers
                         //   printfn "done"
                            
                            return! loop()
                        }
                    loop()

system.WhenTerminated.Wait()
