module Canopy.Mobile.Tests

open canopy.mobile
open System.IO
open Expecto
open System.Collections.Generic
open System.Net
open OpenQA.Selenium.Appium
open OpenQA.Selenium.Appium.Android
open OpenQA.Selenium
open System.Threading
open OpenQA.Selenium.Appium.Android.Enums
open OpenQA.Selenium.Appium.Interfaces

let downloadDemoApp () =
    let localFile = FileInfo("./temp/ApiDemos-debug.apk")
    if File.Exists localFile.FullName then
        printfn "app %s already exists" localFile.FullName
    else
        let appOnServer = "http://appium.github.io/appium/assets/ApiDemos-debug.apk"
        Directory.CreateDirectory localFile.Directory.FullName |> ignore
        use client = new WebClient()
        
        printfn "downloading %s to %s" appOnServer localFile.FullName
        client.DownloadFile(appOnServer, localFile.FullName)
        printfn "app downloaded"

    localFile.FullName

let tests =
    testList "android tests" [
        testList "session tests" [
            testCase "can get device UUID" <| fun () ->
                let deviceUIID = driver.SessionDetails.["deviceUDID"].ToString()
                Expect.isNotNull deviceUIID "deviceUIID was set"

            testCase "can get dictionary data" <| fun () ->
                let dictionary : Dictionary<string, obj> = driver.SessionDetails.["desired"] |> unbox
                Expect.isGreaterThan dictionary.Count 0 "desired data is set"
        ]

        
        testList "device tests" [
            testCase "can get device time" <| fun () ->
                let time = driver.DeviceTime

                Expect.equal time.Length 28 "time has correct format"
        ]
        
        testList "app strings tests" [
            testCase "can get app strings" <| fun () ->
                let appStrings = driver.GetAppStringDictionary()
                Expect.notEqual appStrings.Count 0 "app strings are not empty"

            testCase "can get app strings using lang" <| fun () ->
                let appStrings = driver.GetAppStringDictionary("en")
                Expect.notEqual appStrings.Count 0 "app strings are not empty"
        ]
        
        testList "key press tests" [
            testCase "can press key code" <| fun () ->
                driver.PressKeyCode(AndroidKeyCode.Home)

            testCase "can press key code with meta state" <| fun () ->
                driver.PressKeyCode(AndroidKeyCode.Space, AndroidKeyMetastate.Meta_Shift_On)

            testCase "can long press key code" <| fun () ->
                driver.LongPressKeyCode(AndroidKeyCode.Space)

            testCase "can long press key code with meta state" <| fun () ->
                driver.LongPressKeyCode(AndroidKeyCode.Space, AndroidKeyMetastate.Meta_Shift_On)
        ]

        
        testList "element tests" [
            testCase "can find element by Android UI Automator" <| fun () ->
                driver.StartActivity("io.appium.android.apis", ".ApiDemos")
                let byAndroidUIAutomator = new ByAndroidUIAutomator("new UiSelector().clickable(true)")
                let element = driver.FindElementById("android:id/content").FindElement(byAndroidUIAutomator)

                Expect.isNotNull element.Text "text is set"
                Expect.isGreaterThanOrEqual (driver.FindElementById("android:id/content").FindElements(byAndroidUIAutomator).Count) 1 "selects at least 1 element"
                
            testCase "can find element by XPath" <| fun () ->
                let element = Selector.XPath "//android.widget.TextView[@text='API Demos']" |> find
                Expect.isNotNull element.Text "headline is set"
            
            testCase "can click element by XPath" <| fun () ->
                Selector.XPath "//android.widget.TextView[@text='Graphics']" |> click
                Selector.XPath "//android.widget.TextView[@text='Arcs']" |> click

                back()

                Selector.XPath "//android.widget.TextView[@text='Arcs']" |> click

                back()
                back()

            testCase "can find element by XPath with canopy find" <| fun () ->
                let element = Selector.XPath "//android.widget.TextView[@text='API Demos']" |> find
                Expect.isNotNull element.Text "headline is set"

                let element = Selector.XPath "//android.widget.TextView[contains(@text, \"Animat\")]" |> find
                Expect.equal element.Text "Animation" "test is set"
        ]

        testList "complex android tests" [
            testCase "can find elements" <| fun () ->
                let element = Selector.XPath "//android.widget.TextView[contains(@text, \"Animat\")]" |> find
                Expect.isTrue element.Displayed "element is displayed"

        ]
    ]

[<EntryPoint>]
let main args =
    try
        try
            let app = downloadDemoApp()
            let setings = { DefaultAndroidSettings with AVDName = "AVD_for_Nexus_6_by_Google"}
            start setings app
            runTests { defaultConfig with ``parallel`` = false } tests
        with e ->
            printfn "Error: %s" e.Message
            -1
    finally
        quit()