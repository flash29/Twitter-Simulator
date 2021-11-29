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
let mutable followers = Map.empty
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
                                users <- users.Add(input.[1],rand.Next()|>string)
                                logStatus <- logStatus.Add(input.[1], "LoggedIN")
                                n <- n + 1
                            else if input.[0].CompareTo("Followers")=0 then
                                addFollowers input.[1] |> ignore
                            printfn "This is the message from client %s" msg
                            printfn "This is the users map %A" users
                            printfn "This is the followers of a particular user map %A" followers

                            return! loop()
                        }
                    loop()

system.WhenTerminated.Wait()
