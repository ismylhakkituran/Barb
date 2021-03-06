﻿namespace Barb.Lib

open System
open System.Collections.Generic
open System.Text
open System.IO
open System.Linq

module internal LibInternals = 

    type private TokenizeMode = 
        | Normal
        | Quoted

    // TODO: Change to in terms of stream/seq for quoted newline support
    let tokenizeSVLine (delim: char) = 
        let ignoreQuoted = [| yield delim; yield! Environment.NewLine |]
        fun (line: string) ->
            let getSubstring b e = 
                if b = e then ""
                else line.Substring(b, e - b + 1) 
            let isChar index c = 
                if index < 0 || index >= line.Length then false
                else line.[index] = c
            let rec tokenizeRemainder index strAcc result mode = 
                match index with
                | ci when ci = line.Length -> (strAcc.ToString() :: result)
                | ci -> 
                    let tokenizeNext = tokenizeRemainder (ci + 1) 
                    match line.[ci], mode with
                    |  c , Normal when c = delim -> tokenizeRemainder (ci + 1) (new StringBuilder()) (strAcc.ToString() :: result) Normal
                    | '"', Normal -> tokenizeRemainder (ci + 1) (new StringBuilder()) result Quoted
                    |  c , Quoted when ignoreQuoted.Contains(c) -> tokenizeRemainder (ci + 1) (strAcc.Append(c)) result Quoted
                    | '"', Quoted when isChar (ci + 1) '"' -> tokenizeRemainder (ci + 2) (strAcc.Append('"')) result Quoted
                    | '"', Quoted -> tokenizeRemainder (ci + 1) strAcc result Normal
                    |  c , mode   -> tokenizeRemainder (ci + 1) (strAcc.Append(c)) result mode
            tokenizeRemainder 0 (new StringBuilder()) [] Normal
            |> List.rev

open System.Collections

module Lib = 
    // Tuple Stuff
    let union (t1: IEnumerable) (t2: IEnumerable) = Seq.append (t1 |> Seq.cast<obj>) (t2 |> Seq.cast<obj>) |> Seq.toArray

    let intersection (t1: IEnumerable) (t2: IEnumerable) = 
        seq {
            for i1 in t1 do
                for i2 in t2 do
                    if Barb.Interop.objectsEqualInner i1 i2 then yield i1
        } |> Seq.toArray

    let hasIntersection (t1: IEnumerable) (t2: IEnumerable) =
        let t1obj = t1 |> Seq.cast<obj>
        let t2obj = t2 |> Seq.cast<obj>
        t1obj |> Seq.exists (fun i1 -> t2obj |> Seq.exists (fun i2 -> Barb.Interop.objectsEqualInner i1 i2))

module Table =
    let loadTSV (filename: string) : string [] [] = 
        File.ReadLines filename
        |> Seq.map (LibInternals.tokenizeSVLine '\t')
        |> Seq.map (List.toArray)
        |> Seq.filter (fun l -> l |> Array.length > 0)
        |> Seq.toArray

    let firstRowContaining (table: IEnumerable) (element: string) : string [] = 
        table
        |> Seq.cast<string []>
        |> Seq.tryFind (fun row -> row |> Array.exists (fun e -> Barb.Interop.objectsEqualInner e element))
        |> function | Some (row) -> row | None -> [| |]

    let allRowsContaining (table: IEnumerable) (element: string) : string [] [] = 
        table
        |> Seq.cast<string []>
        |> Seq.filter (fun row -> row |> Array.exists (fun e -> Barb.Interop.objectsEqualInner e element))
        |> Seq.toArray