﻿using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using MultipleClipboards.GlobalResources;

namespace MultipleClipboards.CustomInstallerActions
{
	[RunInstaller(true)]
	public partial class InstallerActions : Installer
	{
		public InstallerActions()
		{
			InitializeComponent();
		}

		public override void Install(IDictionary stateSaver)
		{
			base.Install(stateSaver);

			// Save the target directory for use in later stages of the installer.
			stateSaver.Add("TargetDirectory", this.Context.Parameters["AppTargetDirectory"]);
		}

		public override void Commit(IDictionary savedState)
		{
			base.Commit(savedState);
			BackupExistingAppData();

			// Launch the application.
			string path = string.Concat(savedState["TargetDirectory"], @"\", Constants.ApplicationExecutableName);
			Process.Start(path, "-fromShortcut");
		}

		public override void Uninstall(IDictionary savedState)
		{
			base.Uninstall(savedState);
			PurgeExistingAppData();
		}

		private static void PurgeExistingAppData()
		{
			if (Directory.Exists(Constants.BaseDataPath))
			{
				Directory.Delete(Constants.BaseDataPath, true);
			}
		}

		private static void BackupExistingAppData()
		{
			if (!Directory.Exists(Constants.BackupDataPath))
			{
				Directory.CreateDirectory(Constants.BackupDataPath);
			}

			foreach (var fileName in Directory.GetFiles(Constants.BaseDataPath))
			{
				FileInfo file = new FileInfo(fileName);
				string newFileName = string.Concat(Constants.BackupDataPath, file.Name.Replace(file.Extension, string.Empty), "-", DateTime.Now.ToString("MM-dd-yyyy-HH-mm-ss"), file.Extension);
				file.CopyTo(newFileName);
				
				if (!fileName.Equals(Constants.SettingsFilePath, StringComparison.InvariantCultureIgnoreCase))
				{
					file.Delete();
				}
			}
		}
	}
}
