namespace Liangwengu.Application

open System.Threading.Tasks

module ReminderSettings =
    /// Persist only after authorization; callers keep their previous value on failure.
    let change (requestPermission: unit -> Task<Result<unit, string>>) (save: bool -> Result<unit, string>) enabled =
        task {
            try
                let! permission =
                    if enabled then
                        requestPermission ()
                    else
                        Task.FromResult(Ok())

                match permission with
                | Error message -> return Error message
                | Ok() ->
                    let! result = Task.Run(fun () -> save enabled)
                    return result |> Result.map (fun () -> enabled)
            with ex ->
                return Error ex.Message
        }
