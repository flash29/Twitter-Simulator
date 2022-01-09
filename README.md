Twitter Clone

This project is a Client-Server implementation of a Twitter like engine which is built in F# using AKKA (Actor Model). The communication between the server and client is done using WebSockets built using Suave.io

Server

The server supports the following functionality :
    1. Registers a user.
    2. Loggs in a user.
    3. Allows the user to Tweet.
    4. Allows the user to Mention other users.
    5. Follow other users.
    6. Retrieve tweets of a specific hashtag.
    7. Logging out a user


To start the server use the following command :

    dotnet fsi Program_WebApi.fsx

This will start a server at http://localhost:3000


Client 

To run the client open the index.html file in any web browser. This will run the client
