#nowarn "FS3261"
namespace WorkflowVersioning.FSharp.Versioning

open System
open System.Collections.Generic
open System.Globalization
open Dapr.Workflow.Versioning

type NumericalStrategy() =
    let compareVersions (v1: string) (v2: string) =
        let left = Int32.Parse((if isNull v1 then "0" else v1), CultureInfo.InvariantCulture)
        let right = Int32.Parse((if isNull v2 then "0" else v2), CultureInfo.InvariantCulture)
        left.CompareTo(right)

    interface IWorkflowVersionStrategy with
        member _.TryParse(typeName: string, canonicalName: byref<string>, version: byref<string>) : bool =
            canonicalName <- String.Empty
            version <- String.Empty

            if String.IsNullOrWhiteSpace(typeName) then
                false
            else
                let mutable i = typeName.Length - 1
                while i >= 0 && Char.IsDigit(typeName[i]) do
                    i <- i - 1

                if i < typeName.Length - 1 then
                    canonicalName <- typeName.[..i]
                    version <- typeName.[i + 1..]
                    true
                else
                    canonicalName <- typeName
                    version <- "0"
                    true

        member _.Compare(v1: string, v2: string) : int =
            compareVersions v1 v2

    interface IComparer<string> with
        member _.Compare(x: string, y: string) : int =
            compareVersions x y