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
open System.Text
open System.Diagnostics

let address = "akka.tcp://TwitterServer@localhost:8777/user/Handler"

let Configuration = ConfigurationFactory.ParseString(
    @"akka{
        actor{
            provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
        }
        remote{
            helios.tcp{
                port = 8778
                hostname = localhost
            }
        }
    }"
)

let system = ActorSystem.Create("Client", Configuration)

let server = system.ActorSelection(address)
let nodes = 30000
let rand = System.Random()
let numRequests = 100000

type MainControl =
    | Init
    | FollowerGen
    | TweetGen
type Workers =
    | RegisterUsers
    | AddingFollowers
    | SendingTweets of string
    | SendingReTweets
    | SendingGetMentions
    | SendingGetHashTags
    | SendingGetSubscribedTweets

type Operations =
    | SendLogout


let mutable Nodelist : list<IActorRef> =[]

// let Operations (mailbox: Actor<_>) =
    
//     let rec loop() = actor {
//                 let! message = mailbox.Receive()
//                 match message with
//                 | SendLogout ->
//                     for i in 0..10 do
//                         printfn "hello from 3"

                       
//                 return! loop()
//             }
//     loop()

// let OperationsHandler = spawn system "OperationsHand" Operations
let hashTagsUsed = ["#Liverpool"; "#RealMadrid"; "#Barca"; "#ChampionsLeague"; "#ManchesterUnited"; "#BayernMunich"; "#Juventus"; "#ACMilan"; "#Football"; "#InterMilan"; "#PSG"; "#Aresenal"; "#Tottenham"; "#ManchesterCity"]

let genTweet =
    let mutable tweetGenerated = ""
    let mutable tempHash = rand.Next()
    tempHash <- tempHash % hashTagsUsed.Length
    let userMentioned = "User" + (string (rand.Next()% nodes))
    tweetGenerated <- userMentioned + "-This is awesome-" + hashTagsUsed.Item(tempHash)
    tweetGenerated


let Workers (mailbox: Actor<_>) =
    
    // let mutable hashedValue
    let rec loop() = actor {
                let! message = mailbox.Receive()
                match message with
                | RegisterUsers ->   
                    server <! "Register-"+mailbox.Self.Path.Name
                | AddingFollowers ->
                    server <! "Followers-"+mailbox.Self.Path.Name    
                | SendingTweets(tweet) ->
                    // let newTweet = genTweet
                    // printfn "%s" newTweet
                    server <! "Tweets-"+tweet+"-"+mailbox.Self.Path.Name
                | SendingReTweets ->
                    server <! "ReTweets-"+mailbox.Self.Path.Name
                | SendingGetMentions ->
                    let mentionedUser = rand.Next() % nodes 
                    server <! "GetMentions-"+"User"+(string mentionedUser)
                | SendingGetHashTags ->
                    server <! "GetHashTags-"+hashTagsUsed.Item(rand.Next()%hashTagsUsed.Length )
                | SendingGetSubscribedTweets ->
                    server <! "GetSubscribedTweets-"+"User"+(string (rand.Next()%nodes))            
                return! loop()
            }
    loop()

Nodelist <- [for i in 0..nodes do yield (spawn system ("User"+(string i)) Workers)] 

let MainControl (mailbox: Actor<_>) =
    
    // let mutable hashedValue
    let rec loop() = actor {
                let! message = mailbox.Receive()
                let mutable n = 0
                match message with
                | Init ->   
                    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                    for i in 0..nodes do
                        Nodelist.[i] <! RegisterUsers
                        if i = nodes then
                            mailbox.Self <! FollowerGen
                    stopWatch.Stop()
                    printfn "Total Time taken for Registration %f" stopWatch.Elapsed.TotalMilliseconds
                | FollowerGen ->
                     let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                     for i in 0..nodes do
                        Nodelist.[i] <! AddingFollowers
                        if i = nodes then
                            mailbox.Self <! TweetGen
                     stopWatch.Stop()
                     printfn "Total Time taken for Adding Followers is %f" stopWatch.Elapsed.TotalMilliseconds
                | TweetGen ->
                    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                    let proc = Process.GetCurrentProcess()
                    let cpu_time_stamp = proc.TotalProcessorTime

                    for i in 0..numRequests do
                        let mutable x = rand.Next()
                        x <- x % nodes
                        let mutable tweetGenerated = ""
                        let mutable tempHash = rand.Next()
                        tempHash <- tempHash % hashTagsUsed.Length
                        let userMentioned = "User" + (string (rand.Next()% nodes))
                        tweetGenerated <- "@"+userMentioned + "/This is awesome/" + hashTagsUsed.Item(tempHash)
                      //  printfn "from temp %s" tweetGenerated
                        Nodelist.[x] <! SendingTweets tweetGenerated
                    stopWatch.Stop()
                    printfn "Total Time taken for Tweeting %f" stopWatch.Elapsed.TotalMilliseconds
                    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                    for i in 0..numRequests do
                        let mutable x = rand.Next()
                        x <- x % nodes
                        Nodelist.[x] <! SendingReTweets 
                    stopWatch.Stop()
                    printfn "Total Time taken for Retweeting %f" stopWatch.Elapsed.TotalMilliseconds
                    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                    for i in 0..numRequests do
                        let mutable x = rand.Next()
                        x <- x % nodes
                        Nodelist.[x] <! SendingGetMentions 
                    stopWatch.Stop()
                    printfn "Total Time taken for Getting Mentions %f" stopWatch.Elapsed.TotalMilliseconds
                    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                    for i in 0..numRequests do
                        let mutable x = rand.Next()
                        x <- x % nodes
                        Nodelist.[x] <! SendingGetHashTags
                    stopWatch.Stop()
                    printfn "Total Time taken for Getting HashTags %f" stopWatch.Elapsed.TotalMilliseconds
                    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                    for i in 0..numRequests do
                        let mutable x = rand.Next()
                        x <- x % nodes
                        Nodelist.[x] <! SendingGetSubscribedTweets
                    stopWatch.Stop()
                    printfn "Total Time taken for Getting Subscribed tweets %f" stopWatch.Elapsed.TotalMilliseconds

                    stopWatch.Stop()
 
                    let cpu_time = (proc.TotalProcessorTime-cpu_time_stamp).TotalMilliseconds
                    printfn "CPU time = %dms" (int64 cpu_time)
                    let time_taken = cpu_time / stopWatch.Elapsed.TotalMilliseconds
                    printfn "the total time taken for computing %f" time_taken
                        
                return! loop()
            }
    loop()

let proc = Process.GetCurrentProcess()
let cpu_time_stamp = proc.TotalProcessorTime
let stopWatch = System.Diagnostics.Stopwatch.StartNew()


let MainBoss = spawn system "MainBoss" MainControl
MainBoss <! Init

stopWatch.Stop()
printfn "Total Time taken for Registration %f" stopWatch.Elapsed.TotalMilliseconds
let cpu_time = (proc.TotalProcessorTime-cpu_time_stamp).TotalMilliseconds
printfn "CPU time = %dms" (int64 cpu_time)
let time_taken = cpu_time / stopWatch.Elapsed.TotalMilliseconds
printfn "the total time taken for computing %f" time_taken