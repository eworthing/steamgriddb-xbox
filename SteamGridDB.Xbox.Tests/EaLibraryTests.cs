using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Stores;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Reading EA game names out of the EA app's own install manifests.
    ///
    /// Unlike <see cref="EpicLibrary"/>'s equivalent, all of this is covered: the two parsers are pure,
    /// and the directory walk takes the install root as an argument rather than finding it, so it runs
    /// against a throwaway directory holding real files. Only the step that locates the real root -
    /// reading %ProgramData%\EA Desktop\machine.ini - is uncovered, and the parser it hands its text to
    /// is exercised directly here.
    ///
    /// The XML in these tests is the real shape, trimmed. Both shapes EA writes are here, because both
    /// are installed side by side under the one install root: the 4.0 manifest current games carry,
    /// rooted at DiPManifest with contentIDs/gameTitles as its children, and the 3.0 one the re-released
    /// classics still carry, rooted at game with the title down in metadata/localeInfo. The full files
    /// also carry build metadata, an uninstall block, runtime paths and an install manifest, none of
    /// which this reads.
    /// </summary>
    public class EaLibraryTests
    {
        private const string PvzManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<DiPManifest version=""4.0"">
  <contentIDs><contentID>194814</contentID></contentIDs>
  <gameTitles>
    <gameTitle locale=""en_US"">Plants vs Zombies Battle for Neighborville</gameTitle>
    <gameTitle locale=""fr_FR"">Plants contre Zombies La Bataille de Neighborville</gameTitle>
  </gameTitles>
</DiPManifest>";

        // SimCity 2000 SE as EA actually ships it - the manifest that left it "Unknown" while the two
        // Plants vs Zombies installs beside it resolved
        private const string SimCityManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<game gameVersion=""2.0.0.1"" manifestVersion=""3.0"">
  <contentIDs><contentID>71104</contentID></contentIDs>
  <metadata>
    <localeInfo locale=""en_US"">
      <title>SimCity 2000 Special Edition</title>
      <eula name=""SimCity 2000 Special Edition End User License Agreement"">/Support/eula/en_US_eula.rtf</eula>
    </localeInfo>
  </metadata>
</game>";

        // ---- ParseInstallRoot: machine.ini's configured install location ----

        [Fact]
        public void The_configured_install_root_is_read_from_machine_ini()
        {
            string ini = "machine.downloadinplacedir=D:\\Games\\EA\nmachine.updatebucket=80\n";

            Assert.Equal("D:\\Games\\EA", EaLibrary.ParseInstallRoot(ini));
        }

        [Fact]
        public void The_trailing_separator_EA_writes_is_stripped()
        {
            // StorageFolder.GetFolderFromPathAsync rejects the trailing separator EA writes
            Assert.Equal(
                "C:\\Program Files\\EA Games",
                EaLibrary.ParseInstallRoot("machine.downloadinplacedir=C:\\Program Files\\EA Games\\\n"));
        }

        [Fact]
        public void A_drive_root_keeps_its_separator()
        {
            // "D:" alone means the drive's current directory, not its root - a different place
            Assert.Equal("D:\\", EaLibrary.ParseInstallRoot("machine.downloadinplacedir=D:\\\n"));
        }

        [Fact]
        public void A_value_containing_equals_signs_is_not_truncated()
        {
            // machine.ini holds JSON values next to this key; splitting on every '=' would cut them
            string ini = "machine.telemetry.updatestats={\"a\":\"b=c\"}\nmachine.downloadinplacedir=E:\\EA=Games\n";

            Assert.Equal("E:\\EA=Games", EaLibrary.ParseInstallRoot(ini));
        }

        [Fact]
        public void Carriage_returns_are_not_left_on_the_value()
        {
            Assert.Equal("D:\\EA", EaLibrary.ParseInstallRoot("machine.downloadinplacedir=D:\\EA\r\nmachine.updatebucket=80\r\n"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("machine.updatebucket=80\n")]
        [InlineData("no separator on this line\n")]
        public void Machine_ini_naming_no_install_root_yields_null(string ini)
        {
            Assert.Null(EaLibrary.ParseInstallRoot(ini));
        }

        // ---- ParseInstallerManifest: one game's installerdata.xml ----

        [Fact]
        public void The_content_id_and_English_title_are_read_from_a_manifest()
        {
            EaLibrary.InstallerManifest manifest = EaLibrary.ParseInstallerManifest(PvzManifest);

            Assert.Equal("Plants vs Zombies Battle for Neighborville", manifest.Title);
            Assert.Equal(new[] { "194814" }, manifest.ContentIds);
        }

        [Fact]
        public void The_older_manifest_the_re_released_classics_carry_is_read_too()
        {
            EaLibrary.InstallerManifest manifest = EaLibrary.ParseInstallerManifest(SimCityManifest);

            Assert.Equal("SimCity 2000 Special Edition", manifest.Title);
            Assert.Equal(new[] { "71104" }, manifest.ContentIds);
        }

        [Fact]
        public void The_older_manifest_takes_its_locale_from_the_element_wrapping_the_title()
        {
            // 3.0 tags localeInfo, not the title inside it, so English still has to win here
            string xml = @"<game manifestVersion=""3.0"">
  <contentIDs><contentID>1</contentID></contentIDs>
  <metadata>
    <localeInfo locale=""de_DE""><title>Ein Spiel</title></localeInfo>
    <localeInfo locale=""en_US""><title>A Game</title></localeInfo>
  </metadata>
</game>";

            Assert.Equal("A Game", EaLibrary.ParseInstallerManifest(xml).Title);
        }

        [Fact]
        public void The_older_manifest_with_no_English_title_falls_back_to_the_first_one_it_has()
        {
            string xml = @"<game manifestVersion=""3.0"">
  <contentIDs><contentID>1</contentID></contentIDs>
  <metadata><localeInfo locale=""de_DE""><title>Ein Spiel</title></localeInfo></metadata>
</game>";

            Assert.Equal("Ein Spiel", EaLibrary.ParseInstallerManifest(xml).Title);
        }

        [Fact]
        public void English_is_preferred_over_a_locale_listed_before_it()
        {
            string xml = @"<DiPManifest>
  <contentIDs><contentID>1</contentID></contentIDs>
  <gameTitles>
    <gameTitle locale=""de_DE"">Ein Spiel</gameTitle>
    <gameTitle locale=""en_US"">A Game</gameTitle>
  </gameTitles>
</DiPManifest>";

            // SteamGridDB names games in English, and this name is about to be searched against it
            Assert.Equal("A Game", EaLibrary.ParseInstallerManifest(xml).Title);
        }

        [Fact]
        public void A_manifest_with_no_English_title_falls_back_to_the_first_one_it_has()
        {
            string xml = @"<DiPManifest>
  <contentIDs><contentID>1</contentID></contentIDs>
  <gameTitles><gameTitle locale=""de_DE"">Ein Spiel</gameTitle></gameTitles>
</DiPManifest>";

            Assert.Equal("Ein Spiel", EaLibrary.ParseInstallerManifest(xml).Title);
        }

        [Fact]
        public void Every_content_id_a_manifest_claims_is_returned()
        {
            string xml = @"<DiPManifest>
  <contentIDs><contentID>111</contentID><contentID>222</contentID></contentIDs>
  <gameTitles><gameTitle locale=""en_US"">A Game</gameTitle></gameTitles>
</DiPManifest>";

            Assert.Equal(new[] { "111", "222" }, EaLibrary.ParseInstallerManifest(xml).ContentIds);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("<DiPManifest><contentIDs><contentID>1</contentID>")]
        [InlineData("not xml at all")]
        public void Unreadable_XML_yields_an_empty_manifest_rather_than_throwing(string xml)
        {
            // One game left unnamed, not a failed load for the whole library
            EaLibrary.InstallerManifest manifest = EaLibrary.ParseInstallerManifest(xml);

            Assert.Null(manifest.Title);
            Assert.Empty(manifest.ContentIds);
        }

        [Fact]
        public void A_manifest_with_no_titles_yields_no_title()
        {
            string xml = "<DiPManifest><contentIDs><contentID>1</contentID></contentIDs></DiPManifest>";

            EaLibrary.InstallerManifest manifest = EaLibrary.ParseInstallerManifest(xml);

            Assert.Null(manifest.Title);
            Assert.Equal(new[] { "1" }, manifest.ContentIds);
        }

        // ---- ReadInstallerManifestsAsync: the walk over a real install root ----

        [Fact]
        public async Task Every_installed_game_is_indexed_by_its_content_id()
        {
            using (TempFolder root = new TempFolder())
            {
                WriteGame(root, "PVZ Battle for Neighborville", PvzManifest);
                WriteGame(root, "Some Other Game", @"<DiPManifest>
  <contentIDs><contentID>555</contentID></contentIDs>
  <gameTitles><gameTitle locale=""en_US"">Some Other Game</gameTitle></gameTitles>
</DiPManifest>");

                Dictionary<string, string> map = await EaLibrary.ReadInstallerManifestsAsync(root.Folder);

                Assert.Equal(2, map.Count);
                Assert.Equal("Plants vs Zombies Battle for Neighborville", map["194814"]);
                Assert.Equal("Some Other Game", map["555"]);
            }
        }

        [Fact]
        public async Task Both_manifest_generations_are_indexed_from_the_one_install_root()
        {
            using (TempFolder root = new TempFolder())
            {
                // Exactly how a real install root looks once a re-released classic is installed
                WriteGame(root, "PVZ Battle for Neighborville", PvzManifest);
                WriteGame(root, "SimCity 2000 SE", SimCityManifest);

                Dictionary<string, string> map = await EaLibrary.ReadInstallerManifestsAsync(root.Folder);

                Assert.Equal(2, map.Count);
                Assert.Equal("Plants vs Zombies Battle for Neighborville", map["194814"]);
                Assert.Equal("SimCity 2000 Special Edition", map["71104"]);
            }
        }

        [Fact]
        public async Task A_manifest_claiming_several_content_ids_is_indexed_under_all_of_them()
        {
            using (TempFolder root = new TempFolder())
            {
                WriteGame(root, "A Game", @"<DiPManifest>
  <contentIDs><contentID>111</contentID><contentID>222</contentID></contentIDs>
  <gameTitles><gameTitle locale=""en_US"">A Game</gameTitle></gameTitles>
</DiPManifest>");

                Dictionary<string, string> map = await EaLibrary.ReadInstallerManifestsAsync(root.Folder);

                Assert.Equal("A Game", map["111"]);
                Assert.Equal("A Game", map["222"]);
            }
        }

        [Fact]
        public async Task Directories_that_are_not_EA_games_are_skipped()
        {
            using (TempFolder root = new TempFolder())
            {
                // No __Installer at all, and an __Installer with no manifest in it - a directory that
                // is not an EA game, and one mid-install
                Directory.CreateDirectory(Path.Combine(root.FullPath, "Not A Game"));
                Directory.CreateDirectory(Path.Combine(root.FullPath, "Half Installed", "__Installer"));
                WriteGame(root, "A Game", PvzManifest);

                Dictionary<string, string> map = await EaLibrary.ReadInstallerManifestsAsync(root.Folder);

                Assert.Single(map);
                Assert.True(map.ContainsKey("194814"));
            }
        }

        [Fact]
        public async Task One_unreadable_manifest_does_not_cost_the_others()
        {
            using (TempFolder root = new TempFolder())
            {
                WriteGame(root, "Broken", "<DiPManifest><contentIDs>");
                WriteGame(root, "Fine", PvzManifest);

                Dictionary<string, string> map = await EaLibrary.ReadInstallerManifestsAsync(root.Folder);

                Assert.Equal("Plants vs Zombies Battle for Neighborville", map["194814"]);
            }
        }

        [Fact]
        public async Task An_empty_install_root_yields_an_empty_map()
        {
            using (TempFolder root = new TempFolder())
            {
                Assert.Empty(await EaLibrary.ReadInstallerManifestsAsync(root.Folder));
            }
        }

        [Fact]
        public async Task Content_ids_are_matched_regardless_of_case()
        {
            using (TempFolder root = new TempFolder())
            {
                WriteGame(root, "A Game", @"<DiPManifest>
  <contentIDs><contentID>OFB-EAST:1234</contentID></contentIDs>
  <gameTitles><gameTitle locale=""en_US"">A Game</gameTitle></gameTitles>
</DiPManifest>");

                Dictionary<string, string> map = await EaLibrary.ReadInstallerManifestsAsync(root.Folder);

                Assert.Equal("A Game", map["ofb-east:1234"]);
            }
        }

        /// <summary>
        /// Lays out one game the way EA does: &lt;install root&gt;\&lt;game&gt;\__Installer\installerdata.xml.
        /// </summary>
        private static void WriteGame(TempFolder root, string folderName, string manifestXml)
        {
            string installerFolder = Path.Combine(root.FullPath, folderName, "__Installer");

            Directory.CreateDirectory(installerFolder);
            File.WriteAllText(Path.Combine(installerFolder, "installerdata.xml"), manifestXml);
        }
    }
}
