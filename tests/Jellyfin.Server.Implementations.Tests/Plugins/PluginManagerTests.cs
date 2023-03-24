using System;
using System.IO;
using System.Text.Json;
using Emby.Server.Implementations.Library;
using Emby.Server.Implementations.Plugins;
using Jellyfin.Extensions.Json;
using Jellyfin.Extensions.Json.Converters;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Plugins
{
    public class PluginManagerTests
    {
        private static readonly string _testPathRoot = Path.Combine(Path.GetTempPath(), "jellyfin-test-data");

        [Fact]
        public void SaveManifest_RoundTrip_Success()
        {
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), null!, null!, null!, new Version(1, 0));
            var manifest = new PluginManifest()
            {
                Version = "1.0"
            };

            var tempPath = Path.Combine(_testPathRoot, "manifest-" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);

            Assert.True(pluginManager.SaveManifest(manifest, tempPath));

            var res = pluginManager.LoadManifest(tempPath);

            Assert.Equal(manifest.Category, res.Manifest.Category);
            Assert.Equal(manifest.Changelog, res.Manifest.Changelog);
            Assert.Equal(manifest.Description, res.Manifest.Description);
            Assert.Equal(manifest.Id, res.Manifest.Id);
            Assert.Equal(manifest.Name, res.Manifest.Name);
            Assert.Equal(manifest.Overview, res.Manifest.Overview);
            Assert.Equal(manifest.Owner, res.Manifest.Owner);
            Assert.Equal(manifest.TargetAbi, res.Manifest.TargetAbi);
            Assert.Equal(manifest.Timestamp, res.Manifest.Timestamp);
            Assert.Equal(manifest.Version, res.Manifest.Version);
            Assert.Equal(manifest.Status, res.Manifest.Status);
            Assert.Equal(manifest.AutoUpdate, res.Manifest.AutoUpdate);
            Assert.Equal(manifest.ImagePath, res.Manifest.ImagePath);
            Assert.Equal(manifest.Assemblies, res.Manifest.Assemblies);
        }

        /// <summary>
        ///  Tests safe traversal within the plugin directory.
        /// </summary>
        /// <remarks>
        ///  Note that the '~' literal is generally seen as unsafe when considering
        ///  directory traversal exploits. However, we combine the relative path and the
        ///  plugin directory's path before resolving the full path for comparison.
        ///  Placing this in the "Safe" tests aims to detect when that is no longer
        ///  the case.
        /// </remarks>
        /// <param name="safePath">The safe path to evaluate.</param>
        [Theory]
        [InlineData("./some1.dll")]
        [InlineData("some3.dll")]
        [InlineData("subdir\\..\\some4.dll")]
        [InlineData("subdir\\..\\.\\some4.dll")]
        [InlineData("subdir\\.\\..\\.\\some4.dll")]
        [InlineData("subdir/../some5.dll")]
        [InlineData("subdir/.././some6.dll")]
        [InlineData("subdir/./.././some7.dll")]
        [InlineData("~/some8.dll")]
        [InlineData("....\\..\\....\\..\\some5.dll")] // "...." is a traversal risk if we attempt to replace "..".
        public void Constructor_DiscoversSafePlugin_Status_Active(string safePath)
        {
            var manifest = new PluginManifest
            {
                Id = Guid.NewGuid(),
                Name = "Safe Assembly",
                Assemblies = new string[] { safePath }
            };

            var tempPath = Path.Combine(_testPathRoot, "plugins-" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(tempPath, "safe"));

            var options = GetTestSerializerOptions();

            var data = JsonSerializer.Serialize(manifest, options);

            var metafilePath = Path.Combine(tempPath, "safe", "meta.json");

            File.WriteAllText(metafilePath, data);

            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), null!, null!, tempPath, new Version(1, 0));

            var res = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(metafilePath), options);

            var expectedFullPath = Path.GetFullPath(Path.Combine(tempPath, "safe", safePath)).NormalizePath();

            Assert.NotNull(res);
            Assert.NotEmpty(pluginManager.Plugins);
            Assert.Equal(PluginStatus.Active, res!.Status);
            Assert.Equal(expectedFullPath, pluginManager.Plugins[0].DllFiles[0]);
            Assert.StartsWith(Path.Combine(tempPath, "safe"), expectedFullPath, StringComparison.InvariantCulture);
        }

        /// <summary>
        ///  Tests unsafe attempts to traverse to higher directories.
        /// </summary>
        /// <remarks>
        ///  Attempts to load directories outside of the plugin should be
        ///  constrained. However, relative traversal within the plugin directory should
        ///  be fine. See <see cref="Constructor_DiscoversSafePlugin_Status_Active(string)"/>
        ///  for examples of safe paths.
        /// </remarks>
        /// <param name="unsafePath">The unsafe path to evaluate.</param>
        [Theory]
        [InlineData("/some2.dll")]
        [InlineData(".././.././../some1.dll")]
        [InlineData("..\\.\\..\\.\\..\\some2.dll")]
        [InlineData("../../../../../../some3.dll")]
        [InlineData("..\\..\\..\\..\\..\\some4.dll")]
        [InlineData("\\\\network\\resource.dll")]
        public void Constructor_DiscoversUnsafePlugin_Status_Malfunctioned(string unsafePath)
        {
            var manifest = new PluginManifest
            {
                Id = Guid.NewGuid(),
                Name = "Unsafe Assembly",
                Assemblies = new string[] { unsafePath }
            };

            var tempPath = Path.Combine(_testPathRoot, "plugins-" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(tempPath, "unsafe"));

            var options = GetTestSerializerOptions();

            var data = JsonSerializer.Serialize(manifest, options);

            var metafilePath = Path.Combine(tempPath, "unsafe", "meta.json");

            File.WriteAllText(metafilePath, data);

            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), null!, null!, tempPath, new Version(1, 0));

            var res = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(metafilePath), options);

            Assert.NotNull(res);
            Assert.Empty(pluginManager.Plugins);
            Assert.Equal(PluginStatus.Malfunctioned, res!.Status);
        }

        private JsonSerializerOptions GetTestSerializerOptions()
        {
            var options = new JsonSerializerOptions(JsonDefaults.Options)
            {
                WriteIndented = true
            };

            for (var i = 0; i < options.Converters.Count; i++)
            {
                // Remove the Guid converter for parity with plugin manager.
                if (options.Converters[i] is JsonGuidConverter converter)
                {
                    options.Converters.Remove(converter);
                }
            }

            return options;
        }
    }
}
