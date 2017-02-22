﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Windows;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json.Linq;

#endregion

namespace Hearthstone_Deck_Tracker.FlyoutControls.Options.Tracker
{
	/// <summary>
	/// Interaction logic for TrackerPlugins.xaml
	/// </summary>
	public partial class TrackerPlugins : UserControl
	{
		public TrackerPlugins()
		{
			InitializeComponent();
		}

		private bool Loaded;

		private void GroupBox_Loaded(object sender, RoutedEventArgs e)
		{
			if(Loaded)
				return;
			try
			{
				ListBoxAvailable.ItemsSource = GetPlugins();
			}
			catch (Exception ex)
			{
				Log.Error(ex);
				
			}
		}

		#region Installed


		public void Load()
		{
			ListBoxPlugins.ItemsSource = PluginManager.Instance.Plugins;
			if(ListBoxPlugins.Items.Count > 0)
				ListBoxPlugins.SelectedIndex = 0;
			else
				GroupBoxDetails.Visibility = Visibility.Hidden;
		}

		private void ListBoxPlugins_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		private void ButtonSettings_OnClick(object sender, RoutedEventArgs e)
		{
			(ListBoxPlugins.SelectedItem as PluginWrapper)?.OnButtonPress();
		}

		private void GroupBox_Drop(object sender, DragEventArgs e)
		{
			if(!e.Data.GetDataPresent(DataFormats.FileDrop)) 
				return;
			InstallPlugin((string[])e.Data.GetData(DataFormats.FileDrop));
		}

		private async void InstallPlugin(string[] files)
		{
			var dir = PluginManager.PluginDirectory.FullName;
			try
			{
				var plugins = 0;
				foreach(var pluginPath in files)
				{
					if(pluginPath.EndsWith(".dll"))
					{
						File.Copy(pluginPath, Path.Combine(dir, Path.GetFileName(pluginPath)), true);
						plugins++;
					}
					else if(pluginPath.EndsWith(".zip"))
					{
						var path = Path.Combine(dir, Path.GetFileNameWithoutExtension(pluginPath));
						ZipFile.ExtractToDirectory(pluginPath, path);
						if(Directory.GetDirectories(path).Length == 1 && Directory.GetFiles(path).Length == 0)
						{
							Directory.Delete(path, true);
							ZipFile.ExtractToDirectory(pluginPath, dir);
						}
						plugins++;
					}
				}
				if(plugins <= 0)
					return;
				var result = await Core.MainWindow.ShowMessageAsync("Plugins installed",
					$"Successfully installed {plugins} plugin(s). \n Restart now to take effect?", MessageDialogStyle.AffirmativeAndNegative);

				if(result != MessageDialogResult.Affirmative)
					return;
				Core.MainWindow.Restart();
			}
			catch(Exception ex)
			{
				Log.Error(ex);
				Core.MainWindow.ShowMessage("Error Importing Plugin", $"Please import manually to {dir}.").Forget();
			}
		}

		#endregion



		#region JSON functions

		private List<Plugin> GetPlugins()
		{
			Log.Info("downloading plugin list.");
			var pluginList = new List<Plugin>();
			var wc = new WebClient();
			wc.Headers["User-Agent"] = $"Hearthstone Deck Tracker {Core.Version} @ Hearthsim";
			var json = JObject.Parse(wc.DownloadString("https://raw.githubusercontent.com/HearthSim/HDT-Plugins/master/plugins.json"));
			foreach (var plugin in json["data"])
			{
				var baseUrl = plugin["url"].ToString();
				var releaseUrl = baseUrl + "/releases/latest";
				var Plugin = new Plugin();
				Plugin.Author = plugin["author"].ToString();
				Plugin.Description = plugin["description"].ToString();
				Plugin.Name = plugin["title"].ToString();
				Plugin.ReleaseUrl = releaseUrl;
				pluginList.Add(Plugin);
			}
			return pluginList;
		}

		private string GetAuthor(string releaseData) => JObject.Parse(releaseData)["author"]["login"].ToString();

		private string GetVersion(string releaseData) => JObject.Parse(releaseData)["tag_name"].ToString().Replace("v", "");

		#endregion



		#region Available

		private void ButtonInstall_OnClick(object sender, RoutedEventArgs e)
		{
			try
			{
				var wc = new WebClient();
				wc.Headers["User-Agent"] = $"Hearthstone Deck Tracker {Core.Version} @ Hearthsim";
				var releaseUrl = (ListBoxAvailable.SelectedItem as Plugin).ReleaseUrl;
				var json = JObject.Parse(wc.DownloadString(releaseUrl));
				var downloadUrl = json["assets"].First["browser_download_url"].ToString();
				var downloadFile = Path.Combine(Path.GetTempPath(), downloadUrl.Split('/').Last());
				wc.DownloadFile(downloadUrl, downloadFile);
				var stringList = new string[1] { downloadFile };
				InstallPlugin(stringList);
			}
			catch(Exception ex)
			{

				Log.Error(ex);
			}
		}

		#endregion


	}

	public class Plugin
	{
		public string Name { get; set; }
		public string Author { get; set; }
		public string Description { get; set; }
		public string ReleaseUrl { get; set; }
	}
}