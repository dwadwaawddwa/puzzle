using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Save;

namespace PuzzleStudio.Core.Steam
{
    /// <summary>Text files written next to an export for the Steamworks partner site and SteamPipe uploads.</summary>
    public static class SteamworksFiles
    {
        /// <summary>Save folder of exported games under %USERPROFILE%\AppData\LocalLow (company/product of the Player Template).</summary>
        public const string SaveCompany = "PuzzleStudio";
        public const string SaveProduct = "PuzzleGame";

        // ------------------------------------------------------------------ languages

        static readonly (string code, string steam)[] LanguageTable =
        {
            ("en", "english"), ("fr", "french"), ("de", "german"), ("es", "spanish"), ("it", "italian"),
            ("pt", "portuguese"), ("pt", "brazilian"), ("ja", "japanese"), ("zh", "schinese"), ("zh", "tchinese"),
            ("ru", "russian"), ("pl", "polish"), ("ko", "koreana"), ("nl", "dutch"), ("tr", "turkish"),
        };

        /// <summary>"french" → "fr". Null when unknown.</summary>
        public static string CodeFromSteamLanguage(string steamLanguage)
        {
            if (string.IsNullOrEmpty(steamLanguage)) return null;
            foreach (var (code, steam) in LanguageTable)
                if (string.Equals(steam, steamLanguage, StringComparison.OrdinalIgnoreCase)) return code;
            return null;
        }

        /// <summary>"fr" → "french". Null when unknown.</summary>
        public static string SteamLanguage(string code)
        {
            foreach (var (c, steam) in LanguageTable)
                if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase)) return steam;
            return null;
        }

        // ------------------------------------------------------------------ rich presence

        public static readonly string[] PresenceTokens = { "#Menu", "#Playing", "#Finished" };

        /// <summary>Localization key of a Rich Presence token's text ("#Playing" → "steam.rp.playing").</summary>
        public static string PresenceKey(string token) => "steam.rp." + token.TrimStart('#').ToLowerInvariant();

        /// <summary>
        /// Rich Presence localization file for Steamworks (Community → Rich Presence → upload).
        /// {0}/{1} in the game texts become %level%/%total%.
        /// </summary>
        public static string RichPresenceVdf(string steamLanguage, Func<string, string> text)
        {
            var sb = new StringBuilder();
            sb.Append("\"lang\"\n{\n");
            sb.Append($"\t\"Language\"\t\"{Escape(steamLanguage)}\"\n");
            sb.Append("\t\"Tokens\"\n\t{\n");
            foreach (var token in PresenceTokens)
            {
                string value = text(PresenceKey(token)) ?? token;
                value = value.Replace("{0}", "%level%").Replace("{1}", "%total%");
                sb.Append($"\t\t\"{token}\"\t\"{Escape(value)}\"\n");
            }
            sb.Append("\t}\n}\n");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ SteamPipe

        /// <summary>app_build script for steamcmd: uploads every file of <paramref name="contentRoot"/> to the depot.</summary>
        public static string AppBuildVdf(long appId, long depotId, string description, string contentRoot)
        {
            var sb = new StringBuilder();
            sb.Append("\"AppBuild\"\n{\n");
            sb.Append($"\t\"AppID\"\t\"{appId}\"\n");
            sb.Append($"\t\"Desc\"\t\"{Escape(description)}\"\n");
            sb.Append($"\t\"ContentRoot\"\t\"{Escape(contentRoot)}\"\n");
            sb.Append("\t\"BuildOutput\"\t\".\\output\\\"\n");
            sb.Append("\t\"Depots\"\n\t{\n");
            sb.Append($"\t\t\"{depotId}\"\n\t\t{{\n");
            sb.Append("\t\t\t\"FileMapping\"\n\t\t\t{\n");
            sb.Append("\t\t\t\t\"LocalPath\"\t\"*\"\n");
            sb.Append("\t\t\t\t\"DepotPath\"\t\".\"\n");
            sb.Append("\t\t\t\t\"recursive\"\t\"1\"\n");
            sb.Append("\t\t\t}\n");
            sb.Append("\t\t\t\"FileExclusion\"\t\"steam_appid.txt\"\n");
            sb.Append("\t\t\t\"FileExclusion\"\t\"*.pdb\"\n");
            sb.Append("\t\t}\n\t}\n}\n");
            return sb.ToString();
        }

        /// <summary>Windows batch that runs steamcmd (from the Steamworks SDK) with the app_build script.</summary>
        public static string UploadBat(string appBuildFileName)
        {
            return string.Join("\r\n", new[]
            {
                "@echo off",
                "rem Uploads the game to Steam with steamcmd (Steamworks SDK > tools > ContentBuilder > builder > steamcmd.exe).",
                "rem Then in Steamworks: SteamPipe > Builds > set the new build live on a branch.",
                "setlocal",
                "cd /d \"%~dp0\"",
                "if not defined STEAMCMD set /p STEAMCMD=Path to steamcmd.exe: ",
                "if not exist \"%STEAMCMD%\" (echo steamcmd.exe not found: %STEAMCMD% & pause & exit /b 1)",
                "set /p STEAMUSER=Steam account with upload rights (Steamworks): ",
                $"\"%STEAMCMD%\" +login %STEAMUSER% +run_app_build \"%~dp0{appBuildFileName}\" +quit",
                "pause",
                "",
            });
        }

        // ------------------------------------------------------------------ guide

        /// <summary>Folder that Steam Auto-Cloud must sync (relative to WinAppDataLocalLow).</summary>
        public static string CloudSubdirectory(GamePackData pack) =>
            $"{SaveCompany}/{SaveProduct}/{SaveSystem.SanitizeFolderName(pack.game.title)}";

        /// <summary>README for the Steamworks folder of an export: what to enter where on the partner site.</summary>
        public static string Guide(GamePackData pack, IList<AchievementDef> achievements, string exeName, IEnumerable<string> storeImages)
        {
            long appId = pack.game.steamAppId;
            long depot = pack.steam.EffectiveDepotId(appId);
            var sb = new StringBuilder();
            void L(string line = "") => sb.Append(line).Append("\r\n");

            L($"STEAMWORKS SETUP — {pack.game.title} {pack.game.version}");
            L(new string('=', 60));
            L();
            L($"App ID: {(appId > 0 ? appId.ToString() : "not set (Studio > Steam tab)")}");
            L($"Depot ID: {(depot > 0 ? depot.ToString() : "not set")}");
            L($"Executable: {exeName}.exe (Steamworks > Installation > General > Launch Options)");
            L();
            L("1. UPLOAD THE GAME (SteamPipe)");
            L("   - Download the Steamworks SDK (partner.steamgames.com > Downloads).");
            L("   - Run steampipe\\upload.bat and give it the path of steamcmd.exe");
            L("     (sdk\\tools\\ContentBuilder\\builder\\steamcmd.exe).");
            L("   - Steamworks > SteamPipe > Builds: set the new build live on the \"default\" branch.");
            L("   - Do not upload steam_appid.txt (the script excludes it).");
            L();
            L("2. ACHIEVEMENTS (Steamworks > Stats & Achievements > Achievements)");
            if (achievements.Count == 0) L("   (none)");
            foreach (var a in achievements)
            {
                L($"   - API Name: {a.id}");
                L($"     Name: {a.name}");
                L($"     Description: {a.description}");
                L($"     Hidden: {(a.hidden ? "yes" : "no")}");
                L($"     Icons: achievements\\{a.id}.jpg  /  achievements\\{a.id}_locked.jpg");
            }
            L("   Then click \"Publish\" (Steamworks > Publish) for them to become active.");
            L();
            L("3. RICH PRESENCE (Steamworks > Community > Rich Presence)");
            if (pack.steam.richPresence) L("   Upload every file of the rich_presence folder.");
            else L("   Disabled in the Studio (Steam tab).");
            L();
            L("4. STEAM CLOUD (Steamworks > Cloud > Auto-Cloud)");
            if (pack.steam.cloudEnabled)
            {
                L("   Byte quota: 1000000   Number of files: 10");
                L("   Root: WinAppDataLocalLow");
                L($"   Subdirectory: {CloudSubdirectory(pack)}");
                L("   Pattern: *.json   (Windows, recursive: no)");
            }
            else L("   Disabled in the Studio (Steam tab). Saves stay on the player's PC.");
            L();
            L("5. STORE PAGE (Steamworks > Store Page Admin > Graphical Assets)");
            foreach (var image in storeImages) L($"   - store\\{image}");
            L("   Screenshots: screenshots\\ (at least 5, 1920 x 1080).");
            L("   Client icon: the .ico next to the game folder.");
            L();
            L("6. STEAM DECK (Steamworks > Steam Deck compatibility)");
            L("   - Full controller support: yes (board cursor, menus, pause, every action).");
            L("   - On-screen glyphs: Xbox-style A / B / X / Y / LB / RB (match the Deck), keyboard glyphs only with a keyboard.");
            L("   - Text input: none.  Launcher: none.  Resolution: 1280 x 800 supported (interface 115 % on the Deck).");
            L("   - Recommended Steam Input template: Gamepad.");
            L();
            L("Generated by Puzzle Studio.");
            return sb.ToString();
        }

        /// <summary>Tab-separated table of the achievements (easy to paste into a spreadsheet).</summary>
        public static string AchievementsTable(IList<AchievementDef> achievements)
        {
            var sb = new StringBuilder("API Name\tName\tDescription\tHidden\tRule\tValue\r\n");
            foreach (var a in achievements)
                sb.Append($"{a.id}\t{a.name}\t{a.description}\t{(a.hidden ? 1 : 0)}\t{a.rule}\t{a.value.ToString(CultureInfo.InvariantCulture)}\r\n");
            return sb.ToString();
        }

        /// <summary>Valve's KeyValues files have no escape sequences (paths keep single backslashes): only quotes must go.</summary>
        static string Escape(string s) => (s ?? "").Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");
    }
}
