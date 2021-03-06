﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MultipleClipboards.Persistence
{
    public class DataObject
    {
        private List<DataFormat> allFormats;
        private List<FailedDataFormat> failedDataFormats;
            
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(4000)]
        public string DescriptionText { get; set; }

        public virtual List<DataFormat> AllFormats
        {
            get { return allFormats ?? (allFormats = new List<DataFormat>()); }
            set { allFormats = value; }
        }

        public virtual List<FailedDataFormat> FailedDataFormats
        {
            get { return failedDataFormats ?? (failedDataFormats = new List<FailedDataFormat>()); }
            set { failedDataFormats = value; }
        }
    }
}