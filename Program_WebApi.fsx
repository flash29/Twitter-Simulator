#!/usr/bin/env fsharpi
// #r "./packages/Suave/lib/net40/Suave.dll"
#r "nuget: Suave, 2.6.1"
#r "nuget: FSharp.Json, 0.4.1"
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Utils.Collections
open System.IO
open Suave.Json
open System.Text.Json
open FSharp.Json


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

//list of all the users along with their passwords 
//Key: username value:password
let mutable users = Map.empty

type UserReg = {
    name : string
    password : string
}

let rand = System.Random()


let registeringUsers (userinfo :UserReg) =
    printfn "Here is the registrstion Request %A" userinfo
    users <- users.Add(userinfo.name, userinfo.password)
    printfn "here is the list of users %A" users
    userinfo

let sayHi (userdata :UserReg) =
    printfn "hello new user %s" userdata.name
    userdata

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
       
         

let app =
  choose
    [ GET >=> choose
        [ path "/userinfo" >=> OK "info sent"
          path "/goodbye" >=> OK "Good bye GET" ]
      POST >=> choose
        [ path "/userregistration" >=> AddingUser
          path "/goodbye" >=> OK "Good bye POST" ] ]



startWebServer defaultConfig app


// let app : WebPart =
//   choose [
//     GET >=> path "/home" >=> Files.file "index.html"
//     GET >=> Files.browseHome
//     RequestErrors.NOT_FOUND "Page not found." 
//   ]
// let config =
//   { defaultConfig with homeFolder = Some (Path.GetFullPath "./public/") }

// startWebServer config app