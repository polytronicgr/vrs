﻿// Copyright © 2014 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VirtualRadar.Localisation;
using VirtualRadar.Resources;
using VirtualRadar.Interface;
using VirtualRadar.Interface.View;

namespace VirtualRadar.WinForms.SettingPage
{
    /// <summary>
    /// Presents the data sources values to the user.
    /// </summary>
    public partial class PageDataSources : Page
    {
        public override string PageTitle { get { return Strings.OptionsDataSourcesSheetTitle; } }

        public override Image PageIcon { get { return Images.Notebook16x16; } }

        public PageDataSources()
        {
            InitializeComponent();
        }

        protected override void CreateBindings()
        {
            base.CreateBindings();
            AddBinding(SettingsView, fileDatabaseFileName,              r => r.Configuration.BaseStationSettings.DatabaseFileName,          r => r.FileName);
            AddBinding(SettingsView, folderFlags,                       r => r.Configuration.BaseStationSettings.OperatorFlagsFolder,       r => r.Folder);
            AddBinding(SettingsView, folderSilhouettes,                 r => r.Configuration.BaseStationSettings.SilhouettesFolder,         r => r.Folder);
            AddBinding(SettingsView, folderPictures,                    r => r.Configuration.BaseStationSettings.PicturesFolder,            r => r.Folder);
            AddBinding(SettingsView, checkBoxSearchPictureSubFolders,   r => r.Configuration.BaseStationSettings.SearchPictureSubFolders,   r => r.Checked);
        }

        protected override void AssociateValidationFields()
        {
            base.AssociateValidationFields();
            SetValidationFields(new Dictionary<ValidationField, Control>() {
                { ValidationField.BaseStationDatabase,  fileDatabaseFileName },
                { ValidationField.FlagsFolder,          folderFlags },
                { ValidationField.SilhouettesFolder,    folderSilhouettes },
                { ValidationField.PicturesFolder,       folderPictures },
            });
        }

        protected override void AssociateInlineHelp()
        {
            base.AssociateInlineHelp();
            SetInlineHelp(fileDatabaseFileName,             Strings.DatabaseFileName,           Strings.OptionsDescribeDataSourcesDatabaseFileName);
            SetInlineHelp(folderFlags,                      Strings.FlagsFolder,                Strings.OptionsDescribeDataSourcesFlagsFolder);
            SetInlineHelp(folderSilhouettes,                Strings.SilhouettesFolder,          Strings.OptionsDescribeDataSourcesSilhouettesFolder);
            SetInlineHelp(folderPictures,                   Strings.PicturesFolder,             Strings.OptionsDescribeDataSourcesPicturesFolder);
            SetInlineHelp(checkBoxSearchPictureSubFolders,  Strings.SearchPictureSubFolders,    Strings.OptionsDescribeDataSourcesSearchPictureSubFolders);
        }
    }
}