using BepInEx;
using ComputerInterface.Interfaces;
using System;

namespace MonkeModDownloader.Main;
[BepInPlugin("com.skellon.gorillatag.monkemoddownloader", "MonkeModDownloader", "1.0.0")]
[BepInDependency("tonimacaroni.computerinterface", "1.8.0")]
public class Plugin: BaseUnityPlugin, IComputerModEntry
{
    string IComputerModEntry.EntryName => "Mod Downloader";
    Type IComputerModEntry.EntryViewType => typeof(DownloaderView);
}