// NOTE: The order of references matters here: http://fsprojects.github.io/FSharp.Data.Toolbox/TwitterProvider.html
#I @"./packages/FSharp.Data.Toolbox.Twitter/lib/net40"
#I @"./packages/FSharp.Data/lib/net40"
#r "FSharp.Data.Toolbox.Twitter.dll"
#r "FSharp.Data.dll"
#load @"packages/FsLab/FsLab.fsx"

open System
open System.IO

open FSharp.Data
open FSharp.Data.Toolbox.Twitter

open RProvider
open RProvider.``base``
open RProvider.stats
open RProvider.graphics
open RDotNet
open XPlot.GoogleCharts


// 1. When do people tweet?
// ================================================
let [<Literal>] location = __SOURCE_DIRECTORY__ + @"/data/fsharp_2013-2014.csv"
type Tweets = CsvProvider<location>
let tweetHistory = Tweets.Load(location)

// What is in the table?
for t in tweetHistory.Rows do printfn "%A" t.Text

let tweetsByTime =
    tweetHistory.Rows
    |> Seq.map (fun tweet -> tweet.CreatedDate)

// Day of week
tweetsByTime
|> Seq.countBy (fun t -> t.DayOfWeek)
|> Seq.map (fun (d, n) -> d.ToString(), n)
|> Chart.Column


// By time of day
tweetsByTime
|> Seq.countBy (fun t -> (24 + (t.Hour - 2))%24 ) // utc
|> Seq.sortBy fst
|> Chart.Column

// 2. Growth of the community
// ==================================================

// Get a list of people that tweeted each day
let tweetersByDate =
    tweetHistory.Rows
    |> Seq.map (fun tweet -> tweet.CreatedDate, tweet.FromUserScreenName)
    |> Seq.groupBy (fun (date, author) -> 
        System.DateTime(date.Year, date.Month, date.Day))
    |> Seq.sortBy fst
    |> Seq.map (fun (dt, ts) ->
        let tweeters = ts |> Seq.map (fun (d, author) -> author)
        dt, tweeters)
    |> Array.ofSeq

// Count number of unique people that tweeted each day
let countsByDay =
    tweetersByDate
    |> Array.map (fun (dt, tweeters) -> 
        let count = tweeters |> set |> Set.count |> float
        dt, count)

// Plot
Chart.Scatter(countsByDay)

// RProvider
let dates, counts = Array.unzip countsByDay
let df = 
    R.data_frame(namedParams["Date", box dates; "Count", box counts])

// Some statistics!
R.plot(df)
R.summary(df)

let lm = R.lm(R.as_formula("Count ~ Date"), df)
R.plot(df)
R.abline(lm)

R.summary(lm)

R.plot(lm)

//=========================================
// 3. More network analysis

open RProvider.igraph

let [<Literal>] linkFile = 
    __SOURCE_DIRECTORY__ + "/data/fsharporgConnections.json"
type Links = JsonProvider<linkFile>

let links = Links.Load(linkFile)

let graph = 
    links 
    |> Array.collect (fun link -> 
         [| link.Source; link.Target |] )
    |> R.graph

let pr = R.page_rank(graph).AsList().["vector"]

let names = R.names(pr).GetValue() : string[]
let values = pr.GetValue() : float[]

Array.zip names values
|> Array.sortBy (fun (_, value) -> -value)
|> Seq.take 10
|> Seq.iter (printfn "%A")

// Other graph stats: diameters, cliques
R.get_diameter(graph)
R.largest_cliques(graph)



