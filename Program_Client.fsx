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
let nodes = 10

type MainControl =
    | Init
    | FollowerGen
    | TweetGen
type Workers =
    | RegisterUsers
    | AddingFollowers

let mutable Nodelist : list<IActorRef> =[]

let Workers (mailbox: Actor<_>) =
    
    // let mutable hashedValue
    let rec loop() = actor {
                let! message = mailbox.Receive()
                match message with
                | RegisterUsers ->   
                    server <! "Register-"+mailbox.Self.Path.Name
                | AddingFollowers ->
                    server <! "Followers-"+mailbox.Self.Path.Name    
                        
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
                    for i in 0..nodes do
                        Nodelist.[i] <! RegisterUsers
                        if i = nodes then
                            mailbox.Self <! FollowerGen
                | FollowerGen ->
                     for i in 0..nodes do
                        Nodelist.[i] <! AddingFollowers
                        if i = nodes then
                            mailbox.Self <! TweetGen
                | TweetGen ->
                    printfn "Here in tweet generator"

                        
                return! loop()
            }
    loop()

let MainBoss = spawn system "MainBoss" MainControl
MainBoss <! Init
