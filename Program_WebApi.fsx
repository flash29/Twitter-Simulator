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
open Suave.RequestErrors


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


let rand = System.Random()

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

let app =
  choose
    [ GET >=> choose
        [ path "/userinfo" >=> OK "info sent"
          path "/goodbye" >=> OK "Good bye GET" ]
      POST >=> choose
        [ path "/userregistration" >=> AddingUser
          path "/logging" >=>  LoggingInUser
          path "/logout" >=>  LoggingOutUser
          path "/goodbye" >=> OK "Good bye POST" ] ]



startWebServer defaultConfig app

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