﻿using System.Collections.ObjectModel;
using System.Xml.Serialization;
using MultipleClipboards.Entities;

namespace MultipleClipboards.LegacyPersistence
{
	[XmlRoot(ElementName = "MultipleClipboards", Namespace = "http://www.theobtuseangle.com/schemas/MultipleClipboards", IsNullable = false)]
	public class MultipleClipboardsDataModel
	{
		public MultipleClipboardsDataModel()
		{
			this.ApplicationSettings = new SerializableDictionary<string, dynamic>();
			this.ClipboardDefinitions = new ObservableCollection<ClipboardDefinition>();
		}

		public ObservableCollection<ClipboardDefinition> ClipboardDefinitions
		{
			get;
			set;
		}

		public SerializableDictionary<string, dynamic> ApplicationSettings
		{
			get;
			set;
		}
	}
}
