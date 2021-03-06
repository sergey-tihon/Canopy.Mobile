[<AutoOpen>]
module canopy.mobile.core

open System
open System.IO
open OpenQA.Selenium
open OpenQA.Selenium.Remote
open OpenQA.Selenium.Appium
open OpenQA.Selenium.Appium.Android
open OpenQA.Selenium.Appium.Interfaces
open System.Threading
open canopy
open System.Diagnostics
open System.Text

let mutable driver : AndroidDriver<IWebElement> = null

let mutable private emulatorProcess : Process = null

[<RequireQualifiedAccess>]
type Selector =
| XPath of xpath:string
| Name of name:string

type AndroidSettings = {
    AVDName : string
    Silent : bool
}

let DefaultAndroidSettings = {
    AVDName = null
    Silent = true
}

let androidHome = lazy (
    let androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME")
    if String.IsNullOrEmpty androidHome then
        failwithf "Environment variable ANDROID_HOME is not set."

    if not (Directory.Exists androidHome) then
        failwithf "Environment variable ANDROID_HOME is set to %s. But this directory does not exist." androidHome

    let sdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")
    if String.IsNullOrEmpty sdkRoot then
        Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT",androidHome,EnvironmentVariableTarget.Process)

    androidHome)

/// Checks whether an Android emulator is already running
let isAndroidEmulatorRunning() =
    if not (isNull emulatorProcess) && not emulatorProcess.HasExited then true else
    let emulatorProcesses = 
        Process.GetProcesses()
        |> Array.filter (fun p -> p.ProcessName.StartsWith "qemu-system")

    Array.isEmpty emulatorProcesses |> not

/// Start an emulator process
let startEmulator settings = 
    if isAndroidEmulatorRunning() then printfn "Android emulator is already running" else
    let emulatorToolPath = Path.Combine(androidHome.Force(), "tools", "emulator.exe")
    
    let argsSB = StringBuilder()

    argsSB.AppendFormat(" -avd {0}",settings.AVDName) |> ignore

    if settings.Silent then
        argsSB.Append(" -no-window -no-boot-anim") |> ignore

    let args = argsSB.ToString()

    let pi = ProcessStartInfo(emulatorToolPath,args)
    pi.UseShellExecute <- false
    emulatorProcess <- Process.Start pi
    
    
let getAndroidCapabilities (settings:AndroidSettings) appName =
    startEmulator settings

    let capabilities = DesiredCapabilities()
    capabilities.SetCapability("platformName", "Android")
    capabilities.SetCapability("platformVersion", "6.0")
    capabilities.SetCapability("platform", "Android")
    capabilities.SetCapability("automationName", "Appium")
    capabilities.SetCapability("deviceName", "Android Emulator")
    capabilities.SetCapability("autoLaunch", "true")
    capabilities.SetCapability("deviceReadyTimeout", 1000)
    capabilities.SetCapability("androidDeviceReadyTimeout", 1000)
    capabilities.SetCapability("app", appName)
    capabilities

/// Starts the webdriver with the given app.
let start settings appName =
    let capabilities = getAndroidCapabilities settings appName
    appium.start()

    let testServerAddress = Uri "http://127.0.0.1:4723/wd/hub"
    driver <- new AndroidDriver<IWebElement>(testServerAddress, capabilities, TimeSpan.FromSeconds(120.0))    

    canopy.types.browser <- driver
    printfn "Done starting"

/// Stops the emulator process that was started with canopy mobile
let stopEmulator() =
    if isNull emulatorProcess then () else
    if emulatorProcess.HasExited then () else
    emulatorProcess.Kill()

    let pi = ProcessStartInfo("adb","shell reboot -p")
    pi.UseShellExecute <- false
    let proc = Process.Start pi
    proc.WaitForExit()


/// Quits the web driver and appium
let quit () = 
    if not (isNull driver) then 
        driver.Quit()

    appium.stop()
    stopEmulator()
    
let private findElements' selector = 
    match selector with
    | Selector.XPath xpath -> driver.FindElementsByXPath xpath |> List.ofSeq
    | Selector.Name name -> driver.FindElementsByName name |> List.ofSeq

/// Finds elements on the current page.
let findElements selector reliable timeout =
    try
        if reliable then
            let results = ref []
            wait timeout (fun _ ->
                results := findElements' selector


                not <| List.isEmpty !results)
            !results
        else
            waitResults timeout (fun _ -> findElements' selector)
    with 
    | :? WebDriverTimeoutException -> failwithf "can't find elements with selector: %A" selector

/// Returns all elements that match the given selector.
let findAll selector = findElements selector true configuration.waitTimeout

/// Returns the first element that matches the given selector.
let find selector = findAll selector |> List.head

/// Returns the first element that matches the given selector or None if no such element exists.
let tryFind selector = findAll selector |> List.tryHead

/// Returns true when the selector matches an element on the current page or otherwise false.
let exists selector = findElements selector true configuration.waitTimeout |> List.isEmpty |> not

/// Clicks the first element that's found with the selector
let click selector =
    try
        wait configuration.waitTimeout (fun _ ->
            try 
                (find selector).Click()
                Thread.Sleep configuration.waitAfterClick
                true
            with _ -> false)
    with
    | _ -> failwithf "Failed to click: %A" selector


/// Clicks the Android back button
let back () =
    try
        wait configuration.waitTimeout (fun _ ->
            try 
                driver.PressKeyCode(AndroidKeyCode.Back)
                Thread.Sleep configuration.waitAfterClick
                true
            with _ -> false)
    with
    | _ -> failwithf "Failed to click Android back button"

