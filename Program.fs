open System
open System.IO
open System.Net.Http
open Newtonsoft.Json.Linq

type Mod = {
    Name: string
    Description: string
    DownloadUrl: string
}

let fetchModsJsonAsync () = async {
    use client = new HttpClient()
    try
        let! response = client.GetAsync("https://raw.githubusercontent.com/segadreamcast1/ROMModList/main/mods.json") |> Async.AwaitTask
        if response.IsSuccessStatusCode then
            let! json = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return Some json
        else
            printfn "Failed to fetch mods JSON. HTTP Status: %d" (int response.StatusCode)
            return None
    with
    | ex -> 
        printfn "An error occurred: %s" ex.Message
        return None
}

let fetchAndParseModsJson () =
    match fetchModsJsonAsync () |> Async.RunSynchronously with
    | Some json ->
        try
            let parsedJson = JArray.Parse(json)
            Some(parsedJson)
        with
        | ex -> 
            printfn "Failed to parse mods JSON: %s" ex.Message
            None
    | None -> None

let downloadAndInstallMod modInfo modDirectory =
    let fileName = Path.GetFileName(Uri(modInfo.DownloadUrl).LocalPath)
    let filePath = Path.Combine(modDirectory, fileName)

    async {
        use client = new HttpClient()
        let! response = client.GetAsync(modInfo.DownloadUrl) |> Async.AwaitTask
        if response.IsSuccessStatusCode then
            use! stream = response.Content.ReadAsStreamAsync() |> Async.AwaitTask
            use fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
            stream.CopyTo(fileStream)
            printfn "Downloaded and installed mod: %s to %s" modInfo.Name filePath
        else
            printfn "Failed to download mod: %s. HTTP Status: %d" modInfo.Name (int response.StatusCode)
    } |> Async.RunSynchronously

let uninstallMod (modName: string) modDirectory =
    let files = Directory.GetFiles(modDirectory, "*", SearchOption.AllDirectories)
    let modFiles = files |> Array.filter (fun f -> 
        let fileName = Path.GetFileNameWithoutExtension(f)
        fileName.Equals(modName, StringComparison.OrdinalIgnoreCase))

    if modFiles.Length > 0 then
        modFiles |> Array.iter (fun file ->
            try
                File.Delete(file)
                printfn "Uninstalled mod: %s" file
            with
            | ex -> printfn "Failed to uninstall mod: %s. Error: %s" file ex.Message)
    else
        printfn "Mod '%s' not found in the mod directory: %s" modName modDirectory

let listInstalledMods modDirectory =
    if Directory.Exists(modDirectory) then
        let files = Directory.GetFiles(modDirectory, "*", SearchOption.AllDirectories)
        if files.Length > 0 then
            printfn "Installed Mods:"
            files |> Array.iter (fun file ->
                printfn "- %s" (Path.GetFileName file))
        else
            printfn "No mods installed in the directory: %s" modDirectory
    else
        printfn "The directory %s does not exist." modDirectory

let listAndInstallMods installModName modDirectory =
    match fetchAndParseModsJson() with
    | Some mods ->
        let modList = mods.ToObject<Mod list>()
        if String.IsNullOrEmpty(installModName) then
            printfn "Available Mods:"
            modList |> List.iter (fun m -> printfn "- %s: %s" m.Name m.Description)
        else
            match modList |> List.tryFind (fun m -> m.Name = installModName) with
            | Some modInfo -> downloadAndInstallMod modInfo modDirectory
            | None -> printfn "Mod %s not found. Make sure you typed the name correctly." installModName
    | None -> printfn "No mods available. Please check the mods JSON file and try again."

let searchMods (keyword: string) =
    match fetchAndParseModsJson() with
    | Some mods ->
        let modList = mods.ToObject<Mod list>()
        let filteredMods = 
            modList 
            |> List.filter (fun m -> 
                m.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || 
                m.Description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
        if List.isEmpty filteredMods then
            printfn "No mods found matching '%s'." keyword
        else
            printfn "Search results for '%s':" keyword
            filteredMods |> List.iter (fun m -> printfn "- %s: %s" m.Name m.Description)
    | None -> printfn "No mods available. Please check the mods JSON file and try again."

let mutable modDirectory = Directory.GetCurrentDirectory()

let setModDirectory directory =
    printfn "Setting mod directory to: %s" directory
    if not (Directory.Exists(directory)) then
        Directory.CreateDirectory(directory) |> ignore
    modDirectory <- directory
    printfn "Mod directory set to: %s" directory

let printUsage () =
    printfn "ROM Manager CLI v1.2.0-build7832"
    printfn "Usage:"
    printfn "  list installed    - List installed mods"
    printfn "  list available    - List available mods"
    printfn "  install <modname> - Install a specific mod (case sensitive)"
    printfn "  uninstall <filename> - Uninstall a specific mod (by its filename)"
    printfn "  setdir <path>     - Set the mod installation directory"
    printfn "  search <keyword>  - Search for mods by keyword"
    printfn "  usage             - Display this help message"
    printfn "  exit              - Exit the ROM Manager"

let rec mainLoop () =
    printf "MODMAN> "
    let input = Console.ReadLine().Split(' ')
    match input with
    | [| "list"; "installed" |] ->
        listInstalledMods modDirectory
        mainLoop()
    | [| "list"; "available" |] ->
        listAndInstallMods "" modDirectory
        mainLoop()
    | [| "install"; modName |] ->
        listAndInstallMods modName modDirectory
        mainLoop()
    | [| "uninstall"; modName |] ->
        uninstallMod modName modDirectory
        mainLoop()
    | [| "setdir"; path |] ->
        setModDirectory path
        mainLoop()
    | [| "search"; keyword |] ->
        searchMods keyword
        mainLoop()
    | [| "usage" |] ->
        printUsage()
        mainLoop()
    | [| "exit" |] ->
        printfn "Exiting ROM Manager..."
    | _ ->
        printfn "Invalid command. Did you mean something else?"
        mainLoop()

[<EntryPoint>]
let main argv =
    printUsage()
    mainLoop()
    0
