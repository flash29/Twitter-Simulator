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
let mutable tweets = Map.empty
//list of all the tweets mapped to a specific user
let mutable userTweet = Map.empty
let mutable followers = Map.empty
let mutable mentions = Map.empty
let mutable hashTags = Map.empty
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
    printfn "Here from addFollowers numof follow: %d" numOfFollowers
    for i in 0..numOfFollowers do
        let t = rand.Next() % users.Count
        let u =users.TryFind ("User"+(string t))
        if u <> userID then
            FList.Add("User"+(string t))
    followers <- followers.Add(username, FList)
    printfn "The list of followers to be added are %A" FList
    

let retweets username tweet =
    let mutable FList = new List<string>()
    FList <- userTweet.GetValueOrDefault username
 //   FList <- FList.Add(tweet)
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

let getSubscribedTweets username = 
    let mutable FList = new List<List<string>>()
    let found, value = followers.TryGetValue username
    for i in 0..value.Count do
        let f, tw = userTweet.TryGetValue value.[i]
        //FList <- FList.Add(tw)
        FList.Add(tw)
    FList

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
    // | Tweet of string*string
    | Retweet of string
    | MentionRetrieve of string
    | HashtagRetrieve of string
    | SubscribedRetrieve of string

let OperationsHandler (mailbox: Actor<_>) =
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with 
        | Retweet (username) ->
            let tweet_no = rand.Next() % tweets.Count
            retweets username tweets.[tweet_no]
        | MentionRetrieve (mention) ->
            getMentions mention
        | HashtagRetrieve (hashtag) ->
            getHashTags hashtag
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
                            let response = msg|>string
                            let input = (response).Split '-'
                            if input.[0].CompareTo("Register") = 0 then
                                CreaterHandlerRef <! RegisterUsers (input.[1])
                                logStatus <- logStatus.Add(input.[1], "LoggedIN")
                            else if input.[0].CompareTo("Followers")=0 then
                                CreaterHandlerRef <! FollowersInit(input.[1])
                            else if input.[0].CompareTo("Retweet") = 0 then
                                OperationsHandlerRef <! Retweet input.[1]
                            else if input.[0].CompareTo("GetMentions") = 0 then
                                OperationsHandlerRef <! MentionRetrieve input.[1]
                            else if input.[0].CompareTo("GetHashTags") = 0 then
                                OperationsHandlerRef <! HashtagRetrieve input.[1]
                            else if input.[0].CompareTo("GetSubscribedTweets") = 0 then
                                OperationsHandlerRef <! SubscribedRetrieve input.[1]
                            printfn "This is the message from client %s" msg
                            printfn "This is the users map %A" users
                            printfn "This is the followers of a particular user map %A" followers

                            return! loop()
                        }
                    loop()

system.WhenTerminated.Wait()
