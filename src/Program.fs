﻿/// The core shell loop is defined and run here.
/// It prompts the user for input, then processes the result before repeating.
/// In addition, some ancillary functions like process launching are also defined.

open System
open System.Diagnostics
open System.ComponentModel
open Model
open Builtins
open Terminal
open LineReader
open System.Text

[<EntryPoint>]
let main _ =

    cursor false
    defaultColour ()
    printfn "For a list of commands type '?' or 'help'"

    /// Prints the prompt ('FSH' plus the working dir) and waits for then accepts input from the user.
    let prompt prior = 
        colour "Magenta"
        printf "FSH %s> " (currentDir ())
        cursor true
        defaultColour ()
        // Here is called a special function from LineReader.fs that accepts tabs and the like.
        let read = readLine prior
        cursor false
        read
   
    /// Attempts to run an executable (not a builtin like cd or dir) and to feed the result to the output.
    let launchProcess fileName args =
        let op = 
            new ProcessStartInfo(fileName, args |> String.concat " ",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false)
            |> fun i -> new Process (StartInfo = i)
                
        let outBuilder = new StringBuilder ()
        let errorBuilder = new StringBuilder ()

        op.OutputDataReceived.Add(fun e -> outBuilder.AppendLine e.Data |> ignore)
        op.ErrorDataReceived.Add(fun e -> errorBuilder.AppendLine e.Data |> ignore)

        try
            op.Start () |> ignore

            colour "Green"
            op.BeginOutputReadLine ()
            op.WaitForExit ()
            op.CancelOutputRead ()

            if errorBuilder.Length <> 0 then 
                Error (errorBuilder.ToString()) 
            else 
                Ok (outBuilder.ToString())
        with
            | :? Win32Exception as ex -> // Even on linux/osx, this is the exception thrown.
                Error (sprintf "%s: %s" fileName ex.Message)
    
    let runCommand command args =
        // Help (or ?) are special builtins, not part of the main builtin map (due to loading order).
        if command = "help" || command = "?" then
            help args
        else
            match Map.tryFind command builtinMap with
            | Some f -> 
                f args
            | None -> // If no builtin is found, try to run the users input as a execute process command.
                launchProcess command args

    let processToken lastResult token =
        match lastResult with
        | Error _ -> lastResult
        | Ok s ->
            match token with
            | Command (name, args) ->
                let args = args @ [s]
                runCommand name args
            | Pipe -> 
                lastResult
            | _ -> Ok ""

    /// Tries to follow what the user is wanting to do: run a builtin, or execute a process or execute code for example.
    /// Will chain together piped results, attempting to feed the result of prior to the input of the next
    let processEntered (s : string) =
        if String.IsNullOrWhiteSpace s then () // nothing specified so just loop
        else 
            let parts = parts s
            let tokens = tokens parts

            let output = (Ok "", tokens) ||> List.fold processToken
            match output with 
            | Ok s -> 
                colour "Green"
                printfn "%s" s 
            | Error s -> 
                colour "Red"
                printfn "%s" s 

    /// The coreloop waits for input, runs that input, and repeats. 
    /// It also handles the special exit command, quiting the loop and thus the process.
    let rec coreLoop prior =
        let entered = prompt prior
        if entered.Trim() = "exit" then ()
        else
            processEntered entered
            coreLoop (entered::prior)

    coreLoop []

    0
