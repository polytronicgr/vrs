var __extends = (this && this.__extends) || function (d, b) {
    for (var p in b) if (b.hasOwnProperty(p)) d[p] = b[p];
    function __() { this.constructor = d; }
    d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
};
var VRS;
(function (VRS) {
    VRS.globalOptions = VRS.globalOptions || {};
    VRS.globalOptions.linkSeparator = VRS.globalOptions.linkSeparator || ' : : ';
    VRS.globalOptions.linkClass = VRS.globalOptions.linkClass || 'aircraftLink';
    var LinkRenderHandler = (function () {
        function LinkRenderHandler(settings) {
            if (!settings)
                throw 'You must supply settings';
            if (!settings.linkSite || !VRS.enumHelper.getEnumName(VRS.LinkSite, settings.linkSite))
                throw 'There is no LinkSite called ' + VRS.LinkSite;
            if (settings.displayOrder === undefined)
                throw 'You must provide a display order';
            if (!settings.canLinkAircraft)
                throw 'You must supply a canLinkAircraft callback';
            if (!settings.hasChanged)
                throw 'You must supply a hasChanged';
            if (!settings.title)
                throw 'You must supply a title';
            if (!settings.buildUrl)
                throw 'You must supply the buildUrl callback';
            if (!settings.target) {
                settings.target = function (aircraft) { return settings.linkSite + '-' + aircraft.formatIcao(); };
            }
            this._Settings = settings;
        }
        Object.defineProperty(LinkRenderHandler.prototype, "linkSite", {
            get: function () {
                return this._Settings.linkSite;
            },
            enumerable: true,
            configurable: true
        });
        Object.defineProperty(LinkRenderHandler.prototype, "displayOrder", {
            get: function () {
                return this._Settings.displayOrder;
            },
            enumerable: true,
            configurable: true
        });
        Object.defineProperty(LinkRenderHandler.prototype, "canLinkAircraft", {
            get: function () {
                return this._Settings.canLinkAircraft;
            },
            enumerable: true,
            configurable: true
        });
        Object.defineProperty(LinkRenderHandler.prototype, "hasChanged", {
            get: function () {
                return this._Settings.hasChanged;
            },
            enumerable: true,
            configurable: true
        });
        Object.defineProperty(LinkRenderHandler.prototype, "title", {
            get: function () {
                return this._Settings.title;
            },
            enumerable: true,
            configurable: true
        });
        Object.defineProperty(LinkRenderHandler.prototype, "buildUrl", {
            get: function () {
                return this._Settings.buildUrl;
            },
            enumerable: true,
            configurable: true
        });
        Object.defineProperty(LinkRenderHandler.prototype, "target", {
            get: function () {
                return this._Settings.target;
            },
            enumerable: true,
            configurable: true
        });
        Object.defineProperty(LinkRenderHandler.prototype, "onClick", {
            get: function () {
                return this._Settings.onClick;
            },
            enumerable: true,
            configurable: true
        });
        LinkRenderHandler.prototype.getTitle = function (aircraft) {
            if ($.isFunction(this.title)) {
                return (this.title)(aircraft);
            }
            else {
                return (this.title);
            }
        };
        LinkRenderHandler.prototype.getTarget = function (aircraft) {
            if ($.isFunction(this.target)) {
                return (this.target)(aircraft);
            }
            else {
                return (this.target);
            }
        };
        return LinkRenderHandler;
    })();
    VRS.LinkRenderHandler = LinkRenderHandler;
    VRS.linkRenderHandlers = [
        new VRS.LinkRenderHandler({
            linkSite: VRS.LinkSite.AirportDataDotCom,
            displayOrder: 100,
            canLinkAircraft: function (aircraft) { return aircraft && !!aircraft.registration.val; },
            hasChanged: function (aircraft) { return aircraft.registration.chg; },
            title: 'www.airport-data.com',
            buildUrl: function (aircraft) { return 'http://www.airport-data.com/aircraft/' + VRS.stringUtility.htmlEscape(aircraft.formatRegistration()) + '.html'; },
            target: 'airport-data'
        }),
        new VRS.LinkRenderHandler({
            linkSite: VRS.LinkSite.AirlinersDotNet,
            displayOrder: 200,
            canLinkAircraft: function (aircraft) { return aircraft && !!aircraft.registration.val; },
            hasChanged: function (aircraft) { return aircraft.registration.chg; },
            title: 'www.airliners.net',
            buildUrl: function (aircraft) { return 'http://www.airliners.net/search/photo.search?regsearch=' + VRS.stringUtility.htmlEscape(aircraft.formatRegistration()); },
            target: 'airliners'
        }),
        new VRS.LinkRenderHandler({
            linkSite: VRS.LinkSite.AirframesDotOrg,
            displayOrder: 300,
            canLinkAircraft: function (aircraft) { return aircraft && !!aircraft.registration.val; },
            hasChanged: function (aircraft) { return aircraft.registration.chg; },
            title: 'www.airframes.org',
            buildUrl: function (aircraft) { return 'http://www.airframes.org/reg/' + VRS.stringUtility.htmlEscape(aircraft.formatRegistration(true)); },
            target: 'airframes'
        }),
        new VRS.LinkRenderHandler({
            linkSite: VRS.LinkSite.StandingDataMaintenance,
            displayOrder: -1,
            canLinkAircraft: function (aircraft) { return (!VRS.serverConfig || VRS.serverConfig.routeSubmissionEnabled()) && aircraft && !!aircraft.callsign.val; },
            hasChanged: function (aircraft) { return aircraft.callsign.chg; },
            title: function (aircraft) { return aircraft.hasRoute() ? VRS.$$.SubmitRouteCorrection : VRS.$$.SubmitRoute; },
            buildUrl: function (aircraft) { return 'http://sdm.virtualradarserver.co.uk/Edit/AddCallsigns.aspx?callsigns=' + VRS.stringUtility.htmlEscape(aircraft.formatCallsign(false)); },
            target: 'vrs-sdm'
        })
    ];
    var LinkRenderHandler_AutoRefreshPluginBase = (function (_super) {
        __extends(LinkRenderHandler_AutoRefreshPluginBase, _super);
        function LinkRenderHandler_AutoRefreshPluginBase(settings) {
            _super.call(this, settings);
            this._LinksRendererPlugin = [];
        }
        LinkRenderHandler_AutoRefreshPluginBase.prototype.disposeBase = function () {
            this._LinksRendererPlugin = [];
        };
        LinkRenderHandler_AutoRefreshPluginBase.prototype.addLinksRendererPlugin = function (value) {
            this._LinksRendererPlugin.push(value);
        };
        LinkRenderHandler_AutoRefreshPluginBase.prototype.refreshAircraftLinksPlugin = function () {
            $.each(this._LinksRendererPlugin, function (idx, linksRendererPlugin) {
                linksRendererPlugin.reRender(true);
            });
        };
        return LinkRenderHandler_AutoRefreshPluginBase;
    })(LinkRenderHandler);
    VRS.LinkRenderHandler_AutoRefreshPluginBase = LinkRenderHandler_AutoRefreshPluginBase;
    var AutoSelectLinkRenderHelper = (function (_super) {
        __extends(AutoSelectLinkRenderHelper, _super);
        function AutoSelectLinkRenderHelper(aircraftAutoSelect) {
            _super.call(this, {
                linkSite: VRS.LinkSite.None,
                displayOrder: -1,
                canLinkAircraft: function () { return true; },
                hasChanged: function () { return false; },
                title: function () { return aircraftAutoSelect.getEnabled() ? VRS.$$.DisableAutoSelect : VRS.$$.EnableAutoSelect; },
                buildUrl: function () { return "#"; },
                target: function () { return null; },
                onClick: function (event) {
                    aircraftAutoSelect.setEnabled(!aircraftAutoSelect.getEnabled());
                    aircraftAutoSelect.saveState();
                    event.stopPropagation();
                    return false;
                }
            });
            this._AircraftAutoSelect = aircraftAutoSelect;
            this._AutoSelectEnabledChangedHook = aircraftAutoSelect.hookEnabledChanged(this.autoSelectEnabledChanged, this);
        }
        AutoSelectLinkRenderHelper.prototype.dispose = function () {
            if (this._AutoSelectEnabledChangedHook) {
                this._AircraftAutoSelect.unhook(this._AutoSelectEnabledChangedHook);
                this._AutoSelectEnabledChangedHook = null;
            }
            this._AircraftAutoSelect = null;
            _super.prototype.disposeBase.call(this);
        };
        AutoSelectLinkRenderHelper.prototype.autoSelectEnabledChanged = function () {
            _super.prototype.refreshAircraftLinksPlugin.call(this);
        };
        return AutoSelectLinkRenderHelper;
    })(LinkRenderHandler_AutoRefreshPluginBase);
    VRS.AutoSelectLinkRenderHelper = AutoSelectLinkRenderHelper;
    var CentreOnSelectedAircraftLinkRenderHandler = (function (_super) {
        __extends(CentreOnSelectedAircraftLinkRenderHandler, _super);
        function CentreOnSelectedAircraftLinkRenderHandler(aircraftList, mapPlugin) {
            _super.call(this, {
                linkSite: VRS.LinkSite.None,
                displayOrder: -1,
                canLinkAircraft: function (aircraft) { return aircraft && mapPlugin && aircraftList && aircraft.hasPosition() && !aircraft.positionStale.val; },
                hasChanged: function () { return false; },
                title: function () { return VRS.$$.CentreOnSelectedAircraft; },
                buildUrl: function () { return "#"; },
                target: function () { return null; },
                onClick: function (event) {
                    var selectedAircraft = aircraftList.getSelectedAircraft();
                    mapPlugin.panTo(selectedAircraft.getPosition());
                    event.stopPropagation();
                    return false;
                }
            });
        }
        return CentreOnSelectedAircraftLinkRenderHandler;
    })(LinkRenderHandler);
    VRS.CentreOnSelectedAircraftLinkRenderHandler = CentreOnSelectedAircraftLinkRenderHandler;
    var HideAircraftNotOnMapLinkRenderHandler = (function (_super) {
        __extends(HideAircraftNotOnMapLinkRenderHandler, _super);
        function HideAircraftNotOnMapLinkRenderHandler(aircraftListFetcher) {
            _super.call(this, {
                linkSite: VRS.LinkSite.None,
                displayOrder: -1,
                canLinkAircraft: function () { return true; },
                hasChanged: function () { return false; },
                title: function () { return aircraftListFetcher.getHideAircraftNotOnMap() ? VRS.$$.AllAircraft : VRS.$$.OnlyAircraftOnMap; },
                buildUrl: function () { return '#'; },
                target: function () { return null; },
                onClick: function (event) {
                    aircraftListFetcher.setHideAircraftNotOnMap(!aircraftListFetcher.getHideAircraftNotOnMap());
                    aircraftListFetcher.saveState();
                    event.stopPropagation();
                    return false;
                }
            });
            this._AircraftListFetcher = aircraftListFetcher;
            this._HideAircraftNotOnMapHook = aircraftListFetcher.hookHideAircraftNotOnMapChanged(this.hideAircraftChanged, this);
        }
        HideAircraftNotOnMapLinkRenderHandler.prototype.dispose = function () {
            if (this._HideAircraftNotOnMapHook) {
                this._AircraftListFetcher.unhook(this._HideAircraftNotOnMapHook);
                this._HideAircraftNotOnMapHook = null;
            }
            this._AircraftListFetcher = null;
            _super.prototype.disposeBase.call(this);
        };
        HideAircraftNotOnMapLinkRenderHandler.prototype.hideAircraftChanged = function () {
            _super.prototype.refreshAircraftLinksPlugin.call(this);
        };
        return HideAircraftNotOnMapLinkRenderHandler;
    })(LinkRenderHandler_AutoRefreshPluginBase);
    VRS.HideAircraftNotOnMapLinkRenderHandler = HideAircraftNotOnMapLinkRenderHandler;
    var JumpToAircraftDetailPageRenderHandler = (function (_super) {
        __extends(JumpToAircraftDetailPageRenderHandler, _super);
        function JumpToAircraftDetailPageRenderHandler() {
            _super.call(this, {
                linkSite: VRS.LinkSite.None,
                displayOrder: -1,
                canLinkAircraft: function () { return true; },
                hasChanged: function () { return false; },
                title: function () { return VRS.$$.ShowDetail; },
                buildUrl: function () { return '#'; },
                target: function () { return null; },
                onClick: function (event) {
                    if (VRS.pageManager) {
                        VRS.pageManager.show(VRS.MobilePageName.AircraftDetail);
                    }
                    event.stopPropagation();
                    return false;
                }
            });
        }
        return JumpToAircraftDetailPageRenderHandler;
    })(LinkRenderHandler);
    VRS.JumpToAircraftDetailPageRenderHandler = JumpToAircraftDetailPageRenderHandler;
    var PauseLinkRenderHandler = (function (_super) {
        __extends(PauseLinkRenderHandler, _super);
        function PauseLinkRenderHandler(aircraftListFetcher) {
            _super.call(this, {
                linkSite: VRS.LinkSite.None,
                displayOrder: -1,
                canLinkAircraft: function () { return true; },
                hasChanged: function () { return false; },
                title: function () { return aircraftListFetcher.getPaused() ? VRS.$$.Resume : VRS.$$.Pause; },
                buildUrl: function () { return '#'; },
                target: function () { return null; },
                onClick: function (event) {
                    aircraftListFetcher.setPaused(!aircraftListFetcher.getPaused());
                    event.stopPropagation();
                    return false;
                }
            });
            this._AircraftListFetcher = aircraftListFetcher;
            this._PausedChangedHook = aircraftListFetcher.hookPausedChanged(this.pausedChanged, this);
        }
        PauseLinkRenderHandler.prototype.dispose = function () {
            if (this._PausedChangedHook) {
                this._AircraftListFetcher.unhook(this._PausedChangedHook);
                this._PausedChangedHook = null;
            }
            this._AircraftListFetcher = null;
            _super.prototype.disposeBase.call(this);
        };
        PauseLinkRenderHandler.prototype.pausedChanged = function () {
            _super.prototype.refreshAircraftLinksPlugin.call(this);
        };
        return PauseLinkRenderHandler;
    })(LinkRenderHandler_AutoRefreshPluginBase);
    VRS.PauseLinkRenderHandler = PauseLinkRenderHandler;
    var LinksRenderer = (function () {
        function LinksRenderer() {
        }
        LinksRenderer.prototype.getDefaultAircraftLinkSites = function () {
            var result = [];
            VRS.arrayHelper.select(this.getAircraftLinkHandlers(), function (handler) {
                result.push(handler.linkSite);
            });
            return result;
        };
        LinksRenderer.prototype.findLinkHandler = function (linkSite) {
            var result = null;
            if (linkSite instanceof VRS.LinkRenderHandler) {
                result = linkSite;
            }
            else {
                var length = VRS.linkRenderHandlers.length;
                for (var i = 0; i < length; ++i) {
                    var handler = VRS.linkRenderHandlers[i];
                    if (handler.linkSite === linkSite) {
                        result = handler;
                        break;
                    }
                }
            }
            return result;
        };
        LinksRenderer.prototype.getAircraftLinkHandlers = function () {
            var result = VRS.arrayHelper.filter(VRS.linkRenderHandlers, function (handler) {
                return handler.displayOrder > 0;
            });
            result.sort(function (lhs, rhs) {
                return lhs.displayOrder - rhs.displayOrder;
            });
            return result;
        };
        return LinksRenderer;
    })();
    VRS.LinksRenderer = LinksRenderer;
    VRS.linksRenderer = new VRS.LinksRenderer();
})(VRS || (VRS = {}));
//# sourceMappingURL=linksRenderer.js.map