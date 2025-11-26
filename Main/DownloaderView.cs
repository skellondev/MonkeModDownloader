using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BepInEx;
using ComputerInterface.Enumerations;
using ComputerInterface.Models;
using MonkeModDownloader.Types;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace MonkeModDownloader.Main;

public class DownloaderView: ComputerView
{
    private static int _modIndex;
    private static ModInfo[] _modList = [];
    private static bool Downloading { get; set; }
    private static string ErrorText(string message) => $"<color=#{ColorUtility.ToHtmlStringRGB(Color.darkRed)}>{message}</color>";
    private async void FetchModList()
    {
        try
        {
            if (Downloading) return;
            Text = "Fetching mod list\nThis should only take a few seconds..";
            Downloading = true;
            var req = UnityWebRequest.Get("https://raw.githubusercontent.com/The-Graze/MonkeModInfo/refs/heads/master/modinfo.json");
            
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Text = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.darkRed)}>{req.responseCode} {(HttpStatusCode)req.responseCode}</color>";
                Downloading = false;
                return;
            }
            
            var array = JArray.Parse(req.downloadHandler.text);
            var modList = array.Select(mod => new ModInfo
                {
                    Name = mod["name"]?.ToString() ?? throw new Exception("Could not get name!"),
                    Version = mod["version"]?.ToString() ?? throw new Exception("Could not get version!"),
                    Category = mod["group"]?.ToString() ?? throw new Exception("Could not get category!"),
                    Gitpath = mod["git_path"]?.ToString() ?? throw new Exception("Could not get Github Path!"),
                    DownloadUrl = mod["download_url"]?.ToString() ?? throw new Exception("Could not get download URL!"),
                    Developers = mod["author"]?.ToString() ?? throw new Exception("Could not get category!"),
                    Dependencies = [.. (mod["dependencies"]?.ToArray() ?? []).Select(j => j.ToString())]
                }).ToList();
            
            modList.RemoveAll(m => m.Gitpath.Contains("BepInEx"));
            _modList = [.. modList.OrderBy(v => v.Category)];
            Downloading = false;
            OnKeyPressed(0);
        }
        catch (Exception)
        {
            Text = ErrorText("Unabled to fetch mod list!\nPlease try again later");
            await Task.Delay(1000);
            Downloading = false;
            ReturnToMainMenu();
        }
    }
    public override void OnShow(object[] args) => FetchModList();
    public override async void OnKeyPressed(EKeyboardKey key)
    {
        try
        {
            if (Downloading) return;

            if (key == EKeyboardKey.Back)
            {
                _modIndex = 0;
                _modList = [];
                ReturnToMainMenu();
                return;
            }

            if (_modList.Length == 0)
            {
                FetchModList();
                return;
            }
            
            if (key == EKeyboardKey.Left) _modIndex--;
            if (key == EKeyboardKey.Right) _modIndex++;

            var mod = _modList[_modIndex = Math.Clamp(_modIndex, 0, _modList.Length - 1)];
            Text =
                $"<size=45><color=#{ColorUtility.ToHtmlStringRGB(Color.gray3)}>ENTER: DOWNLOAD\nOPTION 1: VIEW GITHUB\nOPTION 2: REFRESH MOD LIST</color></size>\n\n" +
                string.Join('\n', new object[]
                {
                    $"{mod.Name} V{mod.Version} ({mod.Category})",
                    $"Developers: {mod.Developers}\n\n"
                });
            
            switch (key)
            {
                case EKeyboardKey.Enter:
                    Downloading = true;
                    await DownloadMod(mod);
                    Downloading = false;
                    break;
                case EKeyboardKey.Option1:
                    Application.OpenURL($"https://github.com/{mod.Gitpath}");
                    break;
                case EKeyboardKey.Option2:
                    _modIndex = 0;
                    FetchModList();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"Got exception while handling key {key} :\n" + ex);
        }
    }
    private async Task DownloadMod(ModInfo mod, bool dependency = false)
    {
        try
        {
            Debug.Log($"Starting download for: {mod.Name} V{mod.Version}");
            var req = UnityWebRequest.Get(mod.DownloadUrl);
            await req.SendWebRequest();
            
            if (req.result != UnityWebRequest.Result.Success || req.downloadHandler.data is not { Length: > 0 } bytes)
            {
                Text = ErrorText($"Unable to download {(dependency? "dependency" : "mod")}!\nPlease contact @skellondev on discord for help");
                await Task.Delay(1000);
                Downloading = false;
                OnKeyPressed(0);
                return;
            }
            
            Debug.Log($"Downloaded {bytes.Length} bytes of data");
            var extension = Path.GetExtension(mod.DownloadUrl);
            if (extension == ".zip")
            {
                var temp = Path.GetTempFileName();
                await File.WriteAllBytesAsync(temp, bytes);
                var zip = ZipFile.OpenRead(temp);
                if (!dependency) Text += "Extracting ZIP File to plugins..\n";
                zip.ExtractToDirectory(Paths.PluginPath);
                await Task.Delay(1000);
                zip.Dispose();
                File.Delete(temp);
            }
            else
            {
                var path = $"{Paths.PluginPath}\\{Path.GetFileName(mod.DownloadUrl)}";
                if (File.Exists(path)) File.Delete(path);
                await File.WriteAllBytesAsync(path, bytes);
            }

            if (mod.Dependencies.Length > 0)
            {
                foreach (var s in mod.Dependencies)
                {
                    if (_modList.All(m => m.Name != s))
                    {
                        Text += $"Could not find dependency: {dependency}\n";
                        return;
                    }

                    await DownloadMod(_modList.First(m => m.Name == s), true);
                }
            }

            Text += dependency? $"Downloaded Dependency: {mod.Name} V{mod.Version}\n": $"Successfully Downloaded {mod.Name}!\n";
        }
        catch (Exception ex)
        {
            Text += $"<color=#{ColorUtility.ToHtmlStringRGB(Color.darkRed)}>{ex}</color>\n";
            Debug.LogError($"\n" + ex);
            await Task.Delay(1000);
            Downloading = false;
            OnKeyPressed(0);
        }
    }
}