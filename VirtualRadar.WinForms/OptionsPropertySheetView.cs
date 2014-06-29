﻿// Copyright © 2012 onwards, Andrew Whewell
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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.View;
using VirtualRadar.Localisation;
using VirtualRadar.WinForms.Options;
using System.IO.Ports;
using VirtualRadar.Resources;

namespace VirtualRadar.WinForms
{
    /// <summary>
    /// The default implementation of <see cref="IOptionsView"/>.
    /// </summary>
    public partial class OptionsPropertySheetView : BaseForm, IOptionsView
    {
        #region Private class - DisplayElement
        /// <summary>
        /// Describes a single element to be shown in the right-hand panel of the display.
        /// </summary>
        class DisplayElement
        {
            public ISheet Sheet { get; private set; }

            public ParentPage ParentPage { get; private set; }

            public DisplayElement(ISheet sheet)
            {
                Sheet = sheet;
            }

            public override bool Equals(object obj)
            {
                var result = Object.ReferenceEquals(this, obj);
                if(!result) {
                    var other = obj as DisplayElement;
                    result = Sheet == other.Sheet && ParentPage == other.ParentPage;
                }

                return result;
            }

            public override int GetHashCode()
            {
                return Sheet != null ? Sheet.GetHashCode() : ParentPage != null ? ParentPage.GetHashCode() : 0;
            }

            public DisplayElement(ParentPage parentPage)
            {
                ParentPage = parentPage;
            }

            public void ShowValidationResults(IEnumerable<ValidationResult> validationResults)
            {
                if(Sheet != null)           Sheet.ShowValidationResults(validationResults);
                else if(ParentPage != null) ParentPage.ShowValidationResults(validationResults);
            }
        }
        #endregion

        private List<IUser> _Users = new List<IUser>();
        public IList<IUser> Users { get { return _Users; } }

        #region Fields
        /// <summary>
        /// The object that's controlling this view.
        /// </summary>
        private IOptionsPresenter _Presenter;

        /// <summary>
        /// The object that's handling online help for us.
        /// </summary>
        private OnlineHelpHelper _OnlineHelp;

        // Each of the sheets of configuration options.
        private SheetDataSourceOptionsControl  _DataSourcesSheet =  new SheetDataSourceOptionsControl();
        private SheetRawFeedOptions     _RawFeedSheet = new SheetRawFeedOptions();
        private SheetWebServerOptions   _WebServerSheet = new SheetWebServerOptions();
        private SheetWebSiteOptions     _WebSiteSheet = new SheetWebSiteOptions();
        private SheetGeneralOptions     _GeneralOptions = new SheetGeneralOptions();

        // Each of the parent pages, the containers for variable length collections of child objects in the configuration.
        private ParentPageMergedFeeds           _MergedFeedsPage = new ParentPageMergedFeeds();
        private ParentPageRebroadcastServers    _RebroadcastServersPage = new ParentPageRebroadcastServers();
        private ParentPageReceiverLocations     _ReceiverLocationsPage = new ParentPageReceiverLocations();
        private ParentPageReceivers             _ReceiversPage = new ParentPageReceivers();

        /// <summary>
        /// The list of sheets for each of the main sections, the sheets that represent objects that only appear once in the configuration.
        /// </summary>
        private List<ISheet> _Sheets = new List<ISheet>();

        /// <summary>
        /// The list of parent pages hanging off the sheets.
        /// </summary>
        private List<ParentPage> _ParentPages = new List<ParentPage>();

        /// <summary>
        /// The list of sheets associated with parent pages, those that allow editing of child objects.
        /// </summary>
        private Dictionary<ParentPage, List<ISheet>> _ChildSheets = new Dictionary<ParentPage, List<ISheet>>();

        /// <summary>
        /// The object that manages the tree view's image list for us.
        /// </summary>
        private DynamicImageList _ImageList = new DynamicImageList();

        /// <summary>
        /// The fake password used to determine whether the user has entered a password or not.
        /// </summary>
        /// <remarks>
        /// The original password entered by the user is not known, we only store a hash. This gets written into
        /// the password field and then if the password isn't this value then we know they've entered something.
        /// </remarks>
        private readonly string _SurrogatePassword = new String((char)1, 10);
        
        /// <summary>
        /// True if a save event handler is running, false otherwise.
        /// </summary>
        private bool _IsSaving;

        /// <summary>
        /// True if the last attempt at validation failed, true otherwise.
        /// </summary>
        private bool _LastValidationFailed;

        /// <summary>
        /// The left-hand position of the sheet button as-at the time that the dialog is first shown. The button gets
        /// moved about but this always records where it starts.
        /// </summary>
        private int _SheetButtonDesignLeft;

        /// <summary>
        /// The gap between the sheet button and the delete button.
        /// </summary>
        private int _SheetButtonDesignGap;
        #endregion

        #region Properties
        #region Internal properties
        /// <summary>
        /// Gets or sets the selected sheet.
        /// </summary>
        private ISheet SelectedSheet
        {
            get { return GetSelectedTreeViewNodeTag<ISheet>(treeView); }
            set { SelectTreeViewNodeByTag(treeView, value); }
        }

        /// <summary>
        /// Gets or sets the selected parent page.
        /// </summary>
        private ParentPage SelectedParentPage
        {
            get { return GetSelectedTreeViewNodeTag<ParentPage>(treeView); }
            set { SelectTreeViewNodeByTag(treeView, value); }
        }

        /// <summary>
        /// Gets or sets the selected sheet or parent page.
        /// </summary>
        private DisplayElement SelectedDisplayElement
        {
            get
            {
                var sheet = SelectedSheet;
                var parentPage = SelectedParentPage;
                return sheet == null ? parentPage == null ? null : new DisplayElement(parentPage) : new DisplayElement(sheet);
            }

            set
            {
                if(value == null || (value.Sheet == null && value.ParentPage == null)) SelectedSheet = null;
                else if(value.Sheet != null) SelectedSheet = value.Sheet;
                else if(value.ParentPage != null) SelectedParentPage = value.ParentPage;
            }
        }
        #endregion

        #region General options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool CheckForNewVersions
        {
            get { return _GeneralOptions.CheckForNewVersions; }
            set { _GeneralOptions.CheckForNewVersions = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int CheckForNewVersionsPeriodDays
        {
            get { return _GeneralOptions.CheckForNewVersionsPeriodDays; }
            set { _GeneralOptions.CheckForNewVersionsPeriodDays = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool DownloadFlightRoutes
        {
            get { return _GeneralOptions.DownloadFlightRoutes; }
            set { _GeneralOptions.DownloadFlightRoutes = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int DisplayTimeoutSeconds
        {
            get { return _GeneralOptions.DisplayTimeoutSeconds; }
            set { _GeneralOptions.DisplayTimeoutSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int TrackingTimeoutSeconds
        {
            get { return _GeneralOptions.TrackingTimeoutSeconds; }
            set { _GeneralOptions.TrackingTimeoutSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int ShortTrailLengthSeconds
        {
            get { return _GeneralOptions.ShortTrailLengthSeconds; }
            set { _GeneralOptions.ShortTrailLengthSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool MinimiseToSystemTray
        {
            get { return _GeneralOptions.MinimiseToSystemTray; }
            set { _GeneralOptions.MinimiseToSystemTray = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IList<RebroadcastSettings> RebroadcastSettings { get; private set; }
        #endregion

        #region Audio settings
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool AudioEnabled
        {
            get { return _GeneralOptions.AudioEnabled; }
            set { _GeneralOptions.AudioEnabled = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string TextToSpeechVoice
        {
            get { return _GeneralOptions.TextToSpeechVoice == TextToSpeechVoiceTypeConverter.DefaultVoiceName() ? null : _GeneralOptions.TextToSpeechVoice; }
            set { _GeneralOptions.TextToSpeechVoice = value ?? TextToSpeechVoiceTypeConverter.DefaultVoiceName(); }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int TextToSpeechSpeed
        {
            get { return _GeneralOptions.TextToSpeechSpeed; }
            set { _GeneralOptions.TextToSpeechSpeed = value; }
        }
        #endregion

        #region Data feed options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IList<Receiver> Receivers
        {
            get { return ReceiverTypeConverter.Receivers; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IList<MergedFeed> MergedFeeds
        {
            get { return MergedFeedTypeConverter.MergedFeeds; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string BaseStationDatabaseFileName
        {
            get { return _DataSourcesSheet.DatabaseFileName; }
            set { _DataSourcesSheet.DatabaseFileName = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string OperatorFlagsFolder
        {
            get { return _DataSourcesSheet.FlagsFolder; }
            set { _DataSourcesSheet.FlagsFolder = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string SilhouettesFolder
        {
            get { return _DataSourcesSheet.SilhouettesFolder; }
            set { _DataSourcesSheet.SilhouettesFolder = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string PicturesFolder
        {
            get { return _DataSourcesSheet.PicturesFolder; }
            set { _DataSourcesSheet.PicturesFolder = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool SearchPictureSubFolders
        {
            get { return _DataSourcesSheet.SearchPictureSubFolders; }
            set { _DataSourcesSheet.SearchPictureSubFolders = value; }
        }
        #endregion

        #region Raw decoding options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IList<ReceiverLocation> RawDecodingReceiverLocations
        {
            get { return ReceiverLocationsTypeConverter.ReceiverLocations; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingReceiverRange
        {
            get { return _RawFeedSheet.ReceiverRange; }
            set { _RawFeedSheet.ReceiverRange = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingSuppressReceiverRangeCheck
        {
            get { return _RawFeedSheet.SuppressReceiverRangeCheck; }
            set { _RawFeedSheet.SuppressReceiverRangeCheck = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingIgnoreMilitaryExtendedSquitter
        {
            get { return _RawFeedSheet.IgnoreMilitaryExtendedSquitter; }
            set { _RawFeedSheet.IgnoreMilitaryExtendedSquitter = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingUseLocalDecodeForInitialPosition
        {
            get { return _RawFeedSheet.UseLocalDecodeForInitialPosition; }
            set { _RawFeedSheet.UseLocalDecodeForInitialPosition = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingAirborneGlobalPositionLimit
        {
            get { return _RawFeedSheet.AirborneGlobalPositionLimit; }
            set { _RawFeedSheet.AirborneGlobalPositionLimit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingFastSurfaceGlobalPositionLimit
        {
            get { return _RawFeedSheet.FastSurfaceGlobalPositionLimit; }
            set { _RawFeedSheet.FastSurfaceGlobalPositionLimit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingSlowSurfaceGlobalPositionLimit
        {
            get { return _RawFeedSheet.SlowSurfaceGlobalPositionLimit; }
            set { _RawFeedSheet.SlowSurfaceGlobalPositionLimit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double RawDecodingAcceptableAirborneSpeed
        {
            get { return _RawFeedSheet.AcceptableAirborneSpeed; }
            set { _RawFeedSheet.AcceptableAirborneSpeed = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double RawDecodingAcceptableAirSurfaceTransitionSpeed
        {
            get { return _RawFeedSheet.AcceptableAirSurfaceTransitionSpeed; }
            set { _RawFeedSheet.AcceptableAirSurfaceTransitionSpeed = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double RawDecodingAcceptableSurfaceSpeed
        {
            get { return _RawFeedSheet.AcceptableSurfaceSpeed; }
            set { _RawFeedSheet.AcceptableSurfaceSpeed = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingIgnoreCallsignsInBds20
        {
            get { return _RawFeedSheet.IgnoreCallsignsInBds20; }
            set { _RawFeedSheet.IgnoreCallsignsInBds20 = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInPI0Count
        {
            get { return _RawFeedSheet.AcceptIcaoInPI0Count; }
            set { _RawFeedSheet.AcceptIcaoInPI0Count = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInPI0Seconds
        {
            get { return _RawFeedSheet.AcceptIcaoInPI0Seconds; }
            set { _RawFeedSheet.AcceptIcaoInPI0Seconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInNonPICount
        {
            get { return _RawFeedSheet.AcceptIcaoInNonPICount; }
            set { _RawFeedSheet.AcceptIcaoInNonPICount = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInNonPISeconds
        {
            get { return _RawFeedSheet.AcceptIcaoInNonPISeconds; }
            set { _RawFeedSheet.AcceptIcaoInNonPISeconds = value; }
        }
        #endregion

        #region Web server options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool WebServerUserMustAuthenticate
        {
            get { return _WebServerSheet.UserMustAuthenticate; }
            set { _WebServerSheet.UserMustAuthenticate = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IList<string> WebServerUserIds
        {
            get
            {
                if(_WebServerSheet.PermittedUserIds == null) _WebServerSheet.PermittedUserIds = new List<string>();
                return _WebServerSheet.PermittedUserIds;
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool EnableUPnpFeatures
        {
            get { return _WebServerSheet.EnableUPnpFeatures; }
            set { _WebServerSheet.EnableUPnpFeatures = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool IsOnlyVirtualRadarServerOnLan
        {
            get { return _WebServerSheet.IsOnlyVirtualRadarServerOnLan; }
            set { _WebServerSheet.IsOnlyVirtualRadarServerOnLan = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool AutoStartUPnp
        {
            get { return _WebServerSheet.AutoStartUPnp; }
            set { _WebServerSheet.AutoStartUPnp = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int UPnpPort
        {
            get { return _WebServerSheet.UPnpPort; }
            set { _WebServerSheet.UPnpPort = value; }
        }
        #endregion

        #region Internet client restrictions
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanRunReports
        {
            get { return _WebServerSheet.InternetClientCanRunReports; }
            set { _WebServerSheet.InternetClientCanRunReports = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanPlayAudio
        {
            get { return _WebServerSheet.InternetClientCanPlayAudio; }
            set { _WebServerSheet.InternetClientCanPlayAudio = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanSeePictures
        {
            get { return _WebServerSheet.InternetClientCanSeePictures; }
            set { _WebServerSheet.InternetClientCanSeePictures = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int InternetClientTimeoutMinutes
        {
            get { return _WebServerSheet.InternetClientTimeoutMinutes; }
            set { _WebServerSheet.InternetClientTimeoutMinutes = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanSeeLabels
        {
            get { return _WebServerSheet.InternetClientCanSeeLabels; }
            set { _WebServerSheet.InternetClientCanSeeLabels = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool AllowInternetProximityGadgets
        {
            get { return _WebServerSheet.AllowInternetProximityGadgets; }
            set { _WebServerSheet.AllowInternetProximityGadgets = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanSubmitRoutes
        {
            get { return _WebServerSheet.InternetClientCanSubmitRoutes; }
            set { _WebServerSheet.InternetClientCanSubmitRoutes = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanShowPolarPlots
        {
            get { return _WebServerSheet.InternetClientCanShowPolarPlots; }
            set { _WebServerSheet.InternetClientCanShowPolarPlots = value; }
        }
        #endregion

        #region Web site options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public double InitialGoogleMapLatitude
        {
            get { return _WebSiteSheet.InitialGoogleMapLatitude; }
            set { _WebSiteSheet.InitialGoogleMapLatitude = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double InitialGoogleMapLongitude
        {
            get { return _WebSiteSheet.InitialGoogleMapLongitude; }
            set { _WebSiteSheet.InitialGoogleMapLongitude = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string InitialGoogleMapType
        {
            get { return _WebSiteSheet.InitialGoogleMapType; }
            set { _WebSiteSheet.InitialGoogleMapType = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int InitialGoogleMapZoom
        {
            get { return _WebSiteSheet.InitialGoogleMapZoom; }
            set { _WebSiteSheet.InitialGoogleMapZoom = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int InitialGoogleMapRefreshSeconds
        {
            get { return _WebSiteSheet.InitialGoogleMapRefreshSeconds; }
            set { _WebSiteSheet.InitialGoogleMapRefreshSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int MinimumGoogleMapRefreshSeconds
        {
            get { return _WebSiteSheet.MinimumGoogleMapRefreshSeconds; }
            set { _WebSiteSheet.MinimumGoogleMapRefreshSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public DistanceUnit InitialDistanceUnit
        {
            get { return _WebSiteSheet.InitialDistanceUnit; }
            set { _WebSiteSheet.InitialDistanceUnit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public HeightUnit InitialHeightUnit
        {
            get { return _WebSiteSheet.InitialHeightUnit; }
            set { _WebSiteSheet.InitialHeightUnit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public SpeedUnit InitialSpeedUnit
        {
            get { return _WebSiteSheet.InitialSpeedUnit; }
            set { _WebSiteSheet.InitialSpeedUnit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool PreferIataAirportCodes
        {
            get { return _WebSiteSheet.PreferIataAirportCodes; }
            set { _WebSiteSheet.PreferIataAirportCodes = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool EnableBundling
        {
            get { return _WebSiteSheet.EnableBundling; }
            set { _WebSiteSheet.EnableBundling = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool EnableMinifying
        {
            get { return _WebSiteSheet.EnableMinifying; }
            set { _WebSiteSheet.EnableMinifying = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool EnableCompression
        {
            get { return _WebSiteSheet.EnableCompression; }
            set { _WebSiteSheet.EnableCompression = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public ProxyType ProxyType
        {
            get { return _WebSiteSheet.ProxyType; }
            set { _WebSiteSheet.ProxyType = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int WebSiteReceiverId
        {
            get { return _ReceiversPage.WebSiteReceiverId; }
            set { _ReceiversPage.WebSiteReceiverId = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int ClosestAircraftReceiverId
        {
            get { return _ReceiversPage.ClosestAircraftReceiverId; }
            set { _ReceiversPage.ClosestAircraftReceiverId = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int FlightSimulatorXReceiverId
        {
            get { return _ReceiversPage.FlightSimulatorXReceiverId; }
            set { _ReceiversPage.FlightSimulatorXReceiverId = value; }
        }
        #endregion
        #endregion

        #region Events exposed
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ResetToDefaultsClicked;

        /// <summary>
        /// Raises <see cref="ResetToDefaultsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnResetToDefaultsClicked(EventArgs args)
        {
            if(ResetToDefaultsClicked != null) ResetToDefaultsClicked(this, args);
            sheetHostControl.Refresh();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler SaveClicked;

        /// <summary>
        /// Raises <see cref="SaveClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnSaveClicked(EventArgs args)
        {
            if(SaveClicked != null) {
                var currentIsSaving = _IsSaving;
                try {
                    _IsSaving = true;
                    SaveClicked(this, args);
                } finally {
                    _IsSaving = currentIsSaving;
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler<EventArgs<Receiver>> TestConnectionClicked;

        /// <summary>
        /// Raises <see cref="TestConnectionClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        internal virtual void OnTestBaseStationConnectionSettingsClicked(EventArgs<Receiver> args)
        {
            if(TestConnectionClicked != null) TestConnectionClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler TestTextToSpeechSettingsClicked;

        /// <summary>
        /// Raises <see cref="TestTextToSpeechSettingsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnTestTextToSpeechSettingsClicked(EventArgs args)
        {
            if(TestTextToSpeechSettingsClicked != null) TestTextToSpeechSettingsClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler UseIcaoRawDecodingSettingsClicked;

        /// <summary>
        /// Raises <see cref="UseIcaoRawDecodingSettingsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnUseIcaoRawDecodingSettingsClicked(EventArgs args)
        {
            if(UseIcaoRawDecodingSettingsClicked != null) UseIcaoRawDecodingSettingsClicked(this, args);
            sheetHostControl.Refresh();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler UseRecommendedRawDecodingSettingsClicked;

        /// <summary>
        /// Raises <see cref="UseRecommendedRawDecodingSettingsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnUseRecommendedRawDecodingSettingsClicked(EventArgs args)
        {
            if(UseRecommendedRawDecodingSettingsClicked != null) UseRecommendedRawDecodingSettingsClicked(this, args);
            sheetHostControl.Refresh();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler UpdateReceiverLocationsFromBaseStationDatabaseClicked;

        /// <summary>
        /// Raises <see cref="UpdateReceiverLocationsFromBaseStationDatabaseClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        internal virtual void OnUpdateReceiverLocationsFromBaseStationDatabaseClicked(EventArgs args)
        {
            if(UpdateReceiverLocationsFromBaseStationDatabaseClicked != null) UpdateReceiverLocationsFromBaseStationDatabaseClicked(this, args);
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public OptionsPropertySheetView() : base()
        {
            InitializeComponent();

            MergedFeedTypeConverter.MergedFeeds.Clear();
            ReceiverLocationsTypeConverter.ReceiverLocations.Clear();
            ReceiverTypeConverter.Receivers.Clear();

            labelValidationMessages.Text = "";
            buttonSheetButton.Visible = false;

            RebroadcastSettings = new List<RebroadcastSettings>();

            AttachParentPage(_DataSourcesSheet, _ReceiversPage);
            AttachParentPage(_DataSourcesSheet, _ReceiverLocationsPage);
            AttachParentPage(_DataSourcesSheet, _MergedFeedsPage);
            AttachParentPage(_GeneralOptions, _RebroadcastServersPage);

            _Sheets.AddRange(new ISheet[] {
                _DataSourcesSheet,
                _RawFeedSheet,
                _WebServerSheet,
                _WebSiteSheet,
                _GeneralOptions,
            });
        }

        private void AttachParentPage(ISheet sheet, ParentPage parentPage)
        {
            sheet.Pages.Add(parentPage);
            _ParentPages.Add(parentPage);
        }
        #endregion

        #region ArrangeControls, Populate, RecordInitialValues
        /// <summary>
        /// Moves the controls into their final position.
        /// </summary>
        /// <remarks>
        /// Some of the controls sit on top of each other at run-time, which is messy to do in the designer.
        /// This just moves controls from their designer positions to their finished locations.
        /// </remarks>
        private void ArrangeControls()
        {
            var rawDecodingLinkLabelWidth = Math.Max(linkLabelUseRecommendedSettings.Width, linkLabelUseIcaoSettings.Width);
            linkLabelUseIcaoSettings.Left = buttonSheetButton.Right - rawDecodingLinkLabelWidth;
            linkLabelUseRecommendedSettings.Left = buttonSheetButton.Right - rawDecodingLinkLabelWidth;
            labelValidationMessages.Width = (buttonSheetButton.Left - 6) - labelValidationMessages.Left;
            _SheetButtonDesignLeft = buttonSheetButton.Left;
            _SheetButtonDesignGap = buttonDelete.Left - buttonSheetButton.Right;

            buttonSheetButton.Visible = linkLabelUseIcaoSettings.Visible = linkLabelUseRecommendedSettings.Visible = false;
        }

        /// <summary>
        /// Populates the tree view.
        /// </summary>
        private void PopulateTreeView()
        {
            treeView.Nodes.Clear();
            foreach(var sheet in _Sheets) {
                var sheetNode = treeView.Nodes.Add(sheet.ToString());
                InitialiseTreeNode(sheetNode, sheet);

                foreach(var parentPage in sheet.Pages) {
                    var pageNode = sheetNode.Nodes.Add(parentPage.PageTitle);
                    InitialiseTreeNode(pageNode, parentPage);
                }
            }
        }

        /// <summary>
        /// Records the current values in every property in every sheet as the default value.
        /// </summary>
        private void RecordInitialValues()
        {
            foreach(var sheet in _Sheets.Concat(_ChildSheets.SelectMany(r => r.Value))) {
                sheet.SetInitialValues();
            }

            foreach(var parentPage in _ParentPages) {
                parentPage.SetInitialValues();
            }
        }
        #endregion

        #region DoSelectSheet, DoSelectParentPage
        private void DoSelectElement(DisplayElement displayElement)
        {
            if(displayElement.Sheet != null)            DoSelectSheet(displayElement.Sheet);
            else if(displayElement.ParentPage != null)  DoSelectParentPage(displayElement.ParentPage);
        }

        private void DoSelectSheet(ISheet sheet)
        {
            var sheetControl = sheet as Control;
            if(sheetControl != null) ShowPanel2Control(sheetControl);
            else {
                ShowPanel2Control(sheetHostControl);
                sheetHostControl.Sheet = sheet;
            }

            var sheetButtonText = "";
            var showResetToRecommended = false;
            var showResetToIcaoSettings = false;

            var isChildSheet = IsChildSheet(sheet);
            var parentPage = isChildSheet ? GetParentPageForSheet(sheet) : null;

            if(sheet == _RawFeedSheet) showResetToIcaoSettings = showResetToRecommended = true;
            else if(sheet == _GeneralOptions) sheetButtonText = Strings.TestAudioSettings;
            else if(isChildSheet) sheetButtonText = parentPage.GetSettingButtonText();

            buttonSheetButton.Text = sheetButtonText;
            buttonSheetButton.Visible = !String.IsNullOrEmpty(sheetButtonText);
            buttonDelete.Visible = isChildSheet;
            linkLabelUseIcaoSettings.Visible = showResetToIcaoSettings;
            linkLabelUseRecommendedSettings.Visible = showResetToRecommended;

            buttonSheetButton.Left = buttonDelete.Visible ? _SheetButtonDesignLeft : _SheetButtonDesignLeft + _SheetButtonDesignGap + buttonDelete.Width;

            var optionsPage = sheet as OptionsPage;
            if(optionsPage != null) optionsPage.PageSelected();
        }

        private void DoSelectParentPage(ParentPage page)
        {
            ShowPanel2Control(page);
            buttonDelete.Visible = false;
            buttonSheetButton.Visible = false;
            linkLabelResetToDefaults.Visible = false;
            linkLabelUseIcaoSettings.Visible = false;
            linkLabelUseRecommendedSettings.Visible = false;

            var optionsPage = page as OptionsPage;
            if(optionsPage != null) optionsPage.PageSelected();
        }

        private void ShowPanel2Control(Control control)
        {
            if(!control.Visible) {
                foreach(Control panelControl in splitContainer.Panel2.Controls) {
                    panelControl.Visible = false;
                }
                control.Dock = DockStyle.Fill;
                control.Visible = true;
            }
        }
        #endregion

        #region AddAllParentPageSheets, AddParentPageSheets, GetTreeNodeForParentPageSheet, GetParentPageForSheet, SynchroniseValues
        /// <summary>
        /// Adds sheets below parent page nodes for all of the items in collections.
        /// </summary>
        private void AddAllParentPageSheets()
        {
            foreach(var sheet in _Sheets) {
                foreach(var parentPage in sheet.Pages) {
                    AddParentPageSheets(parentPage);
                }
            }
        }

        /// <summary>
        /// Adds sheets for a single parent page.
        /// </summary>
        /// <param name="parentPage"></param>
        private void AddParentPageSheets(ParentPage parentPage)
        {
            var childSheets = parentPage.Populate();
            _ChildSheets.Add(parentPage, childSheets);

            foreach(var childSheet in childSheets) {
                AddTreeNodeForChildSheet(parentPage, childSheet);
            }
        }

        /// <summary>
        /// Used to inform us that a parent page has created a new sheet.
        /// </summary>
        /// <param name="parentPage"></param>
        /// <param name="childSheet"></param>
        public void ShowNewChildSheet(ParentPage parentPage, ISheet childSheet)
        {
            _ChildSheets[parentPage].Add(childSheet);
            AddTreeNodeForChildSheet(parentPage, childSheet);
            AttachControl(childSheet);
            SelectedSheet = childSheet;
        }

        private TreeNode AddTreeNodeForChildSheet(ParentPage parentPage, ISheet childSheet)
        {
            var result = parentPage.TreeNode.Nodes.Add(childSheet.ToString());
            InitialiseTreeNode(result, childSheet);

            return result;
        }

        private void InitialiseTreeNode(TreeNode treeNode, ISheet sheet)
        {
            var imageIndex = _ImageList.AddImage(sheet.Icon == null ? Images.Transparent_16x16 : sheet.Icon);
            treeNode.ImageIndex = treeNode.SelectedImageIndex = imageIndex;
            treeNode.Tag = sheet;
        }

        private void InitialiseTreeNode(TreeNode treeNode, ParentPage parentPage)
        {
            var imageIndex = _ImageList.AddImage(parentPage.Icon == null ? Images.Transparent_16x16 : parentPage.Icon);
            treeNode.ImageIndex = treeNode.SelectedImageIndex = imageIndex;
            treeNode.Tag = parentPage;
            parentPage.TreeNode = treeNode;
        }

        /// <summary>
        /// Removes a child sheet.
        /// </summary>
        /// <param name="sheet"></param>
        private void DoRemoveChildSheet(ISheet sheet)
        {
            var parentPage = GetParentPageForSheet(sheet);
            if(parentPage != null) {
                var sheetNode = GetTreeNodeForParentPageSheet(parentPage, sheet);
                if(sheetNode != null) parentPage.TreeNode.Nodes.Remove(sheetNode);

                parentPage.RemoveRecordForSheet(sheet);

                var childSheets = _ChildSheets[parentPage];
                childSheets.Remove(sheet);
            }
        }

        /// <summary>
        /// Returns the tree node that corresponds with the sheet passed across for the parent page passed across
        /// or null if the parent page has no such child sheet.
        /// </summary>
        /// <param name="parentPage"></param>
        /// <param name="sheet"></param>
        /// <returns></returns>
        private TreeNode GetTreeNodeForParentPageSheet(ParentPage parentPage, ISheet sheet)
        {
            TreeNode result = null;

            if(parentPage.TreeNode != null) {
                result = parentPage.TreeNode.Nodes.OfType<TreeNode>().FirstOrDefault(r => r.Tag == sheet);
            }

            return result;
        }

        /// <summary>
        /// Selects the sheet under a parent page.
        /// </summary>
        /// <param name="parentPage"></param>
        /// <param name="sheet"></param>
        internal void SelectParentPageSheet(ParentPage parentPage, ISheet sheet)
        {
            var treeNode = GetTreeNodeForParentPageSheet(parentPage, sheet);
            if(treeNode != null) treeView.SelectedNode = treeNode;
        }

        /// <summary>
        /// Returns the child sheet that has this record as a tag or null if no such child sheet exists.
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        internal ISheet GetChildSheetForRecord(object record)
        {
            return _ChildSheets.SelectMany(r => r.Value).FirstOrDefault(r => r.Tag == record);
        }

        /// <summary>
        /// Returns true if the sheet passed across is a child sheet.
        /// </summary>
        /// <param name="sheet"></param>
        /// <returns></returns>
        private bool IsChildSheet(ISheet sheet)
        {
            return _ChildSheets.Any(r => r.Value.Contains(sheet));
        }

        /// <summary>
        /// Returns the <see cref="ParentPage"/> for a sheet or null if no parent page owns the sheet.
        /// </summary>
        /// <param name="sheet"></param>
        /// <returns></returns>
        private ParentPage GetParentPageForSheet(ISheet sheet)
        {
            ParentPage result = _ChildSheets.Where(r => r.Value.Contains(sheet)).Select(r => r.Key).FirstOrDefault();
            return result;
        }

        /// <summary>
        /// Called when a value has been changed on the sheet.
        /// </summary>
        /// <param name="sheet"></param>
        /// <remarks>Only sheets that belong to a parent page, i.e. those for objects that can appear more
        /// than once in the configuration, need to be synchronised - the presenter will pick up all changes
        /// to the main sheets.</remarks>
        private void SynchroniseValues(ISheet sheet)
        {
            var parentPage = GetParentPageForSheet(sheet);
            if(parentPage != null) {
                parentPage.SynchroniseValues(sheet);

                var treeNode = GetTreeNodeForParentPageSheet(parentPage, sheet);
                if(treeNode != null && treeNode.Text != sheet.SheetTitle) {
                    if(treeNode != null) treeNode.Text = sheet.SheetTitle;
                }
            }
        }
        #endregion

        #region AttachAllControls
        /// <summary>
        /// Attaches all sheets, parent pages and sheets under parent pages to the tree view.
        /// </summary>
        private void AttachAllControls()
        {
            foreach(var sheet in _Sheets) {
                AttachControl(sheet);
            }
            foreach(var parentPage in _ParentPages) {
                AttachControl(parentPage);
            }
        }

        /// <summary>
        /// Attaches a parent page and all sheets under the parent page to the tree view.
        /// </summary>
        /// <param name="parentPage"></param>
        /// <returns></returns>
        private void AttachControl(ParentPage parentPage)
        {
            if(parentPage != null) {
                parentPage.Visible = false;
                splitContainer.Panel2.Controls.Add(parentPage);

                List<ISheet> childSheets;
                if(_ChildSheets.TryGetValue(parentPage, out childSheets)) {
                    foreach(var childSheet in childSheets) {
                        AttachControl(childSheet);
                    }
                }
            }
        }

        /// <summary>
        /// Attaches a sheet to the tree view.
        /// </summary>
        /// <param name="sheet"></param>
        private void AttachControl(ISheet sheet)
        {
            var sheetControl = sheet as SheetControl;
            if(sheetControl != null) {
                sheetControl.Visible = false;
                splitContainer.Panel2.Controls.Add(sheetControl);
            }
        }
        #endregion

        #region Presenter helpers
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="voiceNames"></param>
        public void PopulateTextToSpeechVoices(IEnumerable<string> voiceNames)
        {
            TextToSpeechVoiceTypeConverter.PopulateWithVoiceNames(voiceNames);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        public void ShowTestConnectionResults(string message, string title)
        {
            MessageBox.Show(message, title);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="receiverLocations"></param>
        public void MergeBaseStationDatabaseReceiverLocations(IEnumerable<ReceiverLocation> receiverLocations)
        {
            var receiverLocationsParentPage = _Sheets.SelectMany(r => r.Pages).FirstOrDefault(r => r is ParentPageReceiverLocations) as ParentPageReceiverLocations;
            if(receiverLocationsParentPage != null) {
                receiverLocationsParentPage.MergeBaseStationDatabaseReceiverLocations(receiverLocations);

                receiverLocationsParentPage.TreeNode.Nodes.Clear();
                _ChildSheets.Remove(receiverLocationsParentPage);

                AddParentPageSheets(receiverLocationsParentPage);
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="results"></param>
        public override void ShowValidationResults(IEnumerable<ValidationResult> results)
        {
            _LastValidationFailed = false;

            DisplayElement selectedElement = null;
            var allAffectedElements = new Dictionary<DisplayElement, List<ValidationResult>>();
            var messages = new StringBuilder();

            foreach(var result in results.Where(r => !r.IsWarning)) {
                selectedElement = AddValidationMessage(selectedElement, allAffectedElements, messages, result);
            }
            foreach(var result in results.Where(r => r.IsWarning)) {
                AddValidationMessage(selectedElement, allAffectedElements, messages, result);
            }

            labelValidationMessages.Text = messages.ToString();
            if(selectedElement != null && _IsSaving) {
                SelectedDisplayElement = selectedElement;
            }

            foreach(var parentPage in _ParentPages) {
                var displayElement = new DisplayElement(parentPage);
                ShowValidationErrorsForDisplayElement(displayElement, allAffectedElements);
            }

            foreach(var sheet in _Sheets.Concat(_ChildSheets.Values.SelectMany(r => r))) {
                var displayElement = new DisplayElement(sheet);
                ShowValidationErrorsForDisplayElement(displayElement, allAffectedElements);
            }

            if(results.Where(r => !r.IsWarning).Count() > 0) {
                _LastValidationFailed = true;
                DialogResult = DialogResult.None;
            }
        }

        private void ShowValidationErrorsForDisplayElement(DisplayElement displayElement, Dictionary<DisplayElement, List<ValidationResult>> allValidationResults)
        {
            List<ValidationResult> validationResults;
            allValidationResults.TryGetValue(displayElement, out validationResults);
            displayElement.ShowValidationResults(validationResults ?? new List<ValidationResult>());
        }

        private DisplayElement AddValidationMessage(DisplayElement selectedElement, Dictionary<DisplayElement, List<ValidationResult>> allAffectedElements, StringBuilder messages, ValidationResult result)
        {
            if(messages.Length != 0) messages.Append("\r\n");
            messages.AppendFormat("{0}{1}", result.IsWarning ? Localise.GetLocalisedText("::Warning:::") : "", result.Message);

            DisplayElement displayElement = null;
            if(result.Record != null) displayElement = new DisplayElement(GetChildSheetForRecord(result.Record));
            else {
                switch(result.Field) {
                    case ValidationField.BaseStationAddress:
                    case ValidationField.BaseStationPort:
                    case ValidationField.ComPort:
                    case ValidationField.BaudRate:
                    case ValidationField.DataBits:
                    case ValidationField.BaseStationDatabase:
                    case ValidationField.FlagsFolder:
                    case ValidationField.PicturesFolder:
                    case ValidationField.SilhouettesFolder:
                        displayElement = new DisplayElement(_DataSourcesSheet);
                        break;
                    case ValidationField.Location:
                    case ValidationField.ReceiverRange:
                    case ValidationField.AcceptableAirborneLocalPositionSpeed:
                    case ValidationField.AcceptableSurfaceLocalPositionSpeed:
                    case ValidationField.AcceptableTransitionLocalPositionSpeed:
                    case ValidationField.AirborneGlobalPositionLimit:
                    case ValidationField.FastSurfaceGlobalPositionLimit:
                    case ValidationField.SlowSurfaceGlobalPositionLimit:
                    case ValidationField.AcceptIcaoInNonPICount:
                    case ValidationField.AcceptIcaoInNonPISeconds:
                    case ValidationField.AcceptIcaoInPI0Count:
                    case ValidationField.AcceptIcaoInPI0Seconds:
                        displayElement = new DisplayElement(_RawFeedSheet);
                        break;
                    case ValidationField.WebUserName:
                    case ValidationField.UPnpPortNumber:
                    case ValidationField.InternetUserIdleTimeout:
                        displayElement = new DisplayElement(_WebServerSheet);
                        break;
                    case ValidationField.InitialGoogleMapRefreshSeconds:
                    case ValidationField.MinimumGoogleMapRefreshSeconds:
                    case ValidationField.Latitude:
                    case ValidationField.Longitude:
                    case ValidationField.GoogleMapZoomLevel:
                    case ValidationField.SiteRootFolder:
                        displayElement = new DisplayElement(_WebSiteSheet);
                        break;
                    case ValidationField.CheckForNewVersions:
                    case ValidationField.DisplayTimeout:
                    case ValidationField.ShortTrailLength:
                    case ValidationField.TextToSpeechSpeed:
                    case ValidationField.TrackingTimeout:
                        displayElement = new DisplayElement(_GeneralOptions);
                        break;
                    case ValidationField.ClosestAircraftReceiver:
                    case ValidationField.FlightSimulatorXReceiver:
                    case ValidationField.WebSiteReceiver:
                        displayElement = new DisplayElement(_ReceiversPage);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            if(selectedElement == null) selectedElement = displayElement;

            List<ValidationResult> elementResults;
            if(!allAffectedElements.TryGetValue(displayElement, out elementResults)) {
                elementResults = new List<ValidationResult>();
                allAffectedElements.Add(displayElement, elementResults);
            }
            elementResults.Add(result);

            return selectedElement;
        }
        #endregion

        #region Events consumed
        /// <summary>
        /// Called after the form has finished initialising but before it has been shown to the user.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if(!DesignMode) {
                Localise.Form(this);
                treeView.ImageList = _ImageList.ImageList;

                sheetHostControl.OptionsView = this;

                AddAllParentPageSheets();
                ArrangeControls();

                PopulateTreeView();

                _OnlineHelp = new OnlineHelpHelper(this, OnlineHelpAddress.WinFormsOptionsDialog);

                _Presenter = Factory.Singleton.Resolve<IOptionsPresenter>();
                _Presenter.Initialise(this);

                AttachAllControls();

                RecordInitialValues();
                treeView.ExpandAll();
                treeView.SelectedNode = treeView.Nodes.OfType<TreeNode>().FirstOrDefault();
                DoSelectSheet(SelectedSheet);

                OnValueChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Called when an item is selected in the list of sheets.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var selectedSheet = SelectedSheet;
            if(selectedSheet != null) DoSelectSheet(selectedSheet);
            else {
                var selectedPage = SelectedParentPage;
                if(selectedPage != null) DoSelectParentPage(selectedPage);
            }
        }

        /// <summary>
        /// Called when the OK button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            OnSaveClicked(e);

            // We had to set DialogResult to None instead of OK to work around a problem with Mono, whereby the
            // setting of DialogResult immediately calls the FormClosing events and you can't stop the form from
            // closing by just setting DialogResult to None. As a workaround I've added _LastValidationFailed
            // - if that's set to false then we can set DialogResult to OK here.
            if(!_LastValidationFailed) DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// Called when a property is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sheetHostControl_PropertyValueChanged(object sender, SheetEventArgs e)
        {
            SynchroniseValues(e.Sheet);
        }

        /// <summary>
        /// Called when the sheet-specific action button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSheetButton_Click(object sender, EventArgs e)
        {
            var selectedSheet = SelectedSheet;
            if(selectedSheet == _GeneralOptions) OnTestTextToSpeechSettingsClicked(EventArgs.Empty);
            else if(IsChildSheet(selectedSheet)) {
                var parentPage = GetParentPageForSheet(selectedSheet);
                parentPage.SettingButtonClicked(selectedSheet);
            }
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            var selectedSheet = SelectedSheet;
            if(IsChildSheet(selectedSheet)) DoRemoveChildSheet(selectedSheet);
        }

        /// <summary>
        /// Called when the user clicks the 'use ICAO settings' button on the raw decoding property sheet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabelUseIcaoSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnUseIcaoRawDecodingSettingsClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Called when the user clicks the 'use recommended settings' button on the raw decoding sheet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabelUseRecommendedSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnUseRecommendedRawDecodingSettingsClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Called when the user clicks the reset-to-defaults button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabelResetToDefaults_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnResetToDefaultsClicked(EventArgs.Empty);
        }
        #endregion
    }
}
