﻿(function ($, document) {

    function getUserViews(userId) {

        var deferred = $.Deferred();

        ApiClient.getUserViews(userId).done(function (result) {

            var items = result.Items;

            deferred.resolveWith(null, [items]);
        });

        return deferred.promise();
    }

    function createMediaLinks(options) {

        var html = "";

        var items = options.items;

        // "My Library" backgrounds
        for (var i = 0, length = items.length; i < length; i++) {

            var item = items[i];

            var icon;

            switch (item.CollectionType) {
                case "movies":
                    icon = "fa-film";
                    break;
                case "music":
                    icon = "fa-music";
                    break;
                case "photos":
                    icon = "fa-photo";
                    break;
                case "livetv":
                case "tvshows":
                    icon = "fa-video-camera";
                    break;
                case "games":
                    icon = "fa-gamepad";
                    break;
                case "trailers":
                    icon = "fa-film";
                    break;
                case "homevideos":
                    icon = "fa-video-camera";
                    break;
                case "musicvideos":
                    icon = "fa-video-camera";
                    break;
                case "books":
                    icon = "fa-book";
                    break;
                case "channels":
                    icon = "fa-globe";
                    break;
                case "playlists":
                    icon = "fa-list";
                    break;
                default:
                    icon = "fa-folder-o";
                    break;
            }

            var cssClass = "posterItem";
            cssClass += ' ' + options.shape + 'PosterItem';

            if (item.CollectionType) {
                cssClass += ' ' + item.CollectionType + 'PosterItem';
            }

            var href = item.url || LibraryBrowser.getHref(item, options.context);

            html += '<a data-itemid="' + item.Id + '" class="' + cssClass + '" href="' + href + '">';

            var imageCssClass = '';

            html += '<div class="posterItemImage ' + imageCssClass + '">';
            html += '</div>';

            html += "<div class='posterItemDefaultText posterItemText'>";
            html += '<i class="fa ' + icon + '"></i>';
            html += '<span>' + item.Name + '</span>';
            html += "</div>";

            html += "</a>";
        }

        return html;
    }

    function loadlibraryButtons(elem, userId, index) {

        getUserViews(userId).done(function (items) {

            var html = '<br/>';

            if (index) {
                html += '<h1 class="listHeader">' + Globalize.translate('HeaderMyViews') + '</h1>';
            }
            html += '<div>';
            html += createMediaLinks({
                items: items,
                shape: 'myLibrary',
                showTitle: true,
                centerText: true

            });
            html += '</div>';

            $(elem).html(html);

            handleLibraryLinkNavigations(elem);
        });
    }

    function loadRecentlyAdded(elem, userId, context) {

        var options = {

            Limit: 24,
            Fields: "PrimaryImageAspectRatio,SyncInfo",
            IsPlayed: false,
            ImageTypeLimit: 1,
            EnableImageTypes: "Primary,Backdrop,Banner,Thumb"
        };

        return ApiClient.getJSON(ApiClient.getUrl('Users/' + userId + '/Items/Latest', options)).done(function (items) {

            var html = '';

            if (items.length) {
                html += '<div>';
                html += '<h1 style="display:inline-block; vertical-align:middle;" class="listHeader">' + Globalize.translate('HeaderLatestMedia') + '</h1>';
                html += '<a href="mypreferencesdisplay.html" class="accentButton"><i class="fa fa-pencil"></i>' + Globalize.translate('ButtonEdit') + '</a>';
                html += '</div>';
                html += '<div class="itemsContainer">';
                html += LibraryBrowser.getPosterViewHtml({
                    items: items,
                    preferThumb: true,
                    shape: 'backdrop',
                    context: context || 'home',
                    showUnplayedIndicator: false,
                    showChildCountIndicator: true,
                    lazy: true,
                });
                html += '</div>';
            }

            $(elem).html(html).lazyChildren();
            $(elem).createCardMenus();
        });
    }

    function loadLatestChannelMedia(elem, userId) {

        var screenWidth = $(window).width();

        var options = {

            Limit: screenWidth >= 2400 ? 10 : (screenWidth >= 1600 ? 10 : (screenWidth >= 1440 ? 8 : (screenWidth >= 800 ? 7 : 6))),
            Fields: "PrimaryImageAspectRatio,SyncInfo",
            Filters: "IsUnplayed",
            UserId: userId
        };

        ApiClient.getJSON(ApiClient.getUrl("Channels/Items/Latest", options)).done(function (result) {

            var html = '';

            if (result.Items.length) {
                html += '<h1 class="listHeader">' + Globalize.translate('HeaderLatestChannelMedia') + '</h1>';
                html += '<div class="itemsContainer">';
                html += LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    preferThumb: true,
                    shape: 'auto',
                    showTitle: true,
                    centerText: true,
                    lazy: true
                });
                html += '</div>';
            }

            $(elem).html(html).lazyChildren();
            $(elem).createCardMenus();
        });
    }

    function loadLibraryTiles(elem, userId, shape, index, autoHideOnMobile) {

        if (autoHideOnMobile) {
            $(elem).addClass('hiddenSectionOnMobile');
        } else {
            $(elem).removeClass('hiddenSectionOnMobile');
        }

        getUserViews(userId).done(function (items) {

            var html = '';

            if (items.length) {

                var cssClass = index !== 0 ? 'listHeader' : 'listHeader firstListHeader';

                html += '<div>';
                html += '<h1 style="display:inline-block; vertical-align:middle;" class="' + cssClass + '">' + Globalize.translate('HeaderMyViews') + '</h1>';
                html += '<a href="mypreferencesdisplay.html" class="accentButton"><i class="fa fa-pencil"></i>' + Globalize.translate('ButtonEdit') + '</a>';
                html += '</div>';

                html += '<div>';
                html += LibraryBrowser.getPosterViewHtml({
                    items: items,
                    shape: shape,
                    showTitle: true,
                    centerText: true,
                    lazy: true,
                    autoThumb: true,
                    context: 'home'
                });
                html += '</div>';
            }


            $(elem).html(html).lazyChildren().createCardMenus();

            handleLibraryLinkNavigations(elem);
        });
    }

    function loadLibraryFolders(elem, userId, shape, index) {

        ApiClient.getItems(userId, {

            SortBy: "SortName",
            ImageTypeLimit: 1

        }).done(function (result) {

            var html = '';
            var items = result.Items;

            for (var i = 0, length = items.length; i < length; i++) {
                items[i].url = 'itemlist.html?parentid=' + items[i].Id;
            }

            if (items.length) {

                html += '<h1 class="listHeader">' + Globalize.translate('HeaderLibraryFolders') + '</h1>';

                html += '<div>';
                html += LibraryBrowser.getPosterViewHtml({
                    items: items,
                    shape: shape,
                    showTitle: true,
                    centerText: true,
                    lazy: true
                });
                html += '</div>';
            }

            $(elem).html(html).lazyChildren();

            handleLibraryLinkNavigations(elem);
        });
    }

    function loadResume(elem, userId) {

        var screenWidth = $(window).width();

        var options = {

            SortBy: "DatePlayed",
            SortOrder: "Descending",
            MediaTypes: "Video",
            Filters: "IsResumable",
            Limit: screenWidth >= 1920 ? 10 : (screenWidth >= 1600 ? 8 : (screenWidth >= 1200 ? 9 : 6)),
            Recursive: true,
            Fields: "PrimaryImageAspectRatio,SyncInfo",
            CollapseBoxSetItems: false,
            ExcludeLocationTypes: "Virtual",
            ImageTypeLimit: 1,
            EnableImageTypes: "Primary,Backdrop,Banner,Thumb"
        };

        ApiClient.getItems(userId, options).done(function (result) {

            var html = '';

            if (result.Items.length) {
                html += '<h1 class="listHeader">' + Globalize.translate('HeaderResume') + '</h1>';
                html += '<div>';
                html += LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    preferThumb: true,
                    shape: 'backdrop',
                    overlayText: true,
                    showTitle: true,
                    showParentTitle: true,
                    context: 'home',
                    lazy: true
                });
                html += '</div>';
            }

            $(elem).html(html).lazyChildren();
            $(elem).createCardMenus();
        });
    }

    function handleLibraryLinkNavigations(elem) {

        $('a.posterItem', elem).on('click', function () {

            var textElem = $('.posterItemText span', this);

            if (!textElem.length) {
                textElem = $('.posterItemText', this);
            }
            var text = textElem.html();

            LibraryMenu.setText(text);
        });
    }

    function loadLatestChannelItems(elem, userId, options) {

        options = $.extend(options || {}, {

            UserId: userId,
            SupportsLatestItems: true
        });

        ApiClient.getJSON(ApiClient.getUrl("Channels", options)).done(function (result) {

            var channels = result.Items;

            var channelsHtml = channels.map(function (c) {

                return '<div id="channel' + c.Id + '"></div>';

            }).join('');

            $(elem).html(channelsHtml);

            for (var i = 0, length = channels.length; i < length; i++) {

                var channel = channels[i];

                loadLatestChannelItemsFromChannel(elem, channel, i);
            }

        });
    }

    function loadLatestChannelItemsFromChannel(page, channel, index) {

        var screenWidth = $(window).width();

        var options = {

            Limit: screenWidth >= 1600 ? 10 : (screenWidth >= 1440 ? 5 : (screenWidth >= 800 ? 6 : 6)),
            Fields: "PrimaryImageAspectRatio,SyncInfo",
            Filters: "IsUnplayed",
            UserId: Dashboard.getCurrentUserId(),
            ChannelIds: channel.Id
        };

        ApiClient.getJSON(ApiClient.getUrl("Channels/Items/Latest", options)).done(function (result) {

            var html = '';

            if (result.Items.length) {

                var cssClass = index !== 0 ? 'listHeader' : 'listHeader firstListHeader';

                html += '<div>';
                var text = Globalize.translate('HeaderLatestFromChannel').replace('{0}', channel.Name);
                html += '<h1 style="display:inline-block; vertical-align:middle;" class="' + cssClass + '">' + text + '</h1>';
                html += '<a href="channelitems.html?context=channels&id=' + channel.Id + '" data-role="button" data-icon="arrow-r" data-mini="true" data-inline="true" data-iconpos="notext" class="sectionHeaderButton"></a>';
                html += '</div>';
            }
            html += '<div class="itemsContainer">';
            html += LibraryBrowser.getPosterViewHtml({
                items: result.Items,
                shape: 'autohome',
                defaultShape: 'square',
                showTitle: true,
                centerText: true,
                context: 'channels',
                lazy: true
            });
            html += '</div>';

            var elem = $('#channel' + channel.Id + '', page).html(html).lazyChildren().trigger('create');
            $(elem).createCardMenus();
        });
    }

    function loadLatestLiveTvRecordings(elem, userId, index) {

        ApiClient.getLiveTvRecordings({

            userId: userId,
            limit: 5,
            IsInProgress: false

        }).done(function (result) {

            var html = '';

            if (result.Items.length) {

                var cssClass = index !== 0 ? 'listHeader' : 'listHeader firstListHeader';

                html += '<div>';
                html += '<h1 style="display:inline-block; vertical-align:middle;" class="' + cssClass + '">' + Globalize.translate('HeaderLatestTvRecordings') + '</h1>';
                html += '<a href="livetvrecordings.html?context=livetv" data-role="button" data-icon="arrow-r" data-mini="true" data-inline="true" data-iconpos="notext" class="sectionHeaderButton"></a>';
                html += '</div>';
            }

            var screenWidth = $(window).width();

            html += LibraryBrowser.getPosterViewHtml({
                items: result.Items,
                shape: "autohome",
                showTitle: true,
                showParentTitle: true,
                overlayText: screenWidth >= 600,
                coverImage: true,
                lazy: true
            });

            elem.html(html).lazyChildren().trigger('create');

        });
    }

    window.Sections = {
        loadRecentlyAdded: loadRecentlyAdded,
        loadLatestChannelMedia: loadLatestChannelMedia,
        loadLibraryTiles: loadLibraryTiles,
        loadLibraryFolders: loadLibraryFolders,
        loadResume: loadResume,
        loadLatestChannelItems: loadLatestChannelItems,
        loadLatestLiveTvRecordings: loadLatestLiveTvRecordings,
        loadlibraryButtons: loadlibraryButtons
    };

})(jQuery, document);

(function ($, document) {

    function getDefaultSection(index) {

        switch (index) {

            case 0:
                return 'librarytiles-automobile';
            case 1:
                return 'resume';
            case 2:
                return 'latestmedia';
            case 3:
                return '';
            default:
                return '';
        }

    }

    function loadSection(page, userId, displayPreferences, index) {

        var section = displayPreferences.CustomPrefs['home' + index] || getDefaultSection(index);

        var elem = $('.section' + index, page);

        if (section == 'latestmedia') {
            Sections.loadRecentlyAdded(elem, userId);
        }
        else if (section == 'librarytiles') {
            Sections.loadLibraryTiles(elem, userId, 'backdrop', index);
        }
        else if (section == 'smalllibrarytiles') {
            Sections.loadLibraryTiles(elem, userId, 'homePageSmallBackdrop', index);
        }
        else if (section == 'smalllibrarytiles-automobile') {
            Sections.loadLibraryTiles(elem, userId, 'homePageSmallBackdrop', index, true);
        }
        else if (section == 'librarytiles-automobile') {
            Sections.loadLibraryTiles(elem, userId, 'backdrop', index, true);
        }
        else if (section == 'librarybuttons') {
            Sections.loadlibraryButtons(elem, userId, index);
        }
        else if (section == 'resume') {
            Sections.loadResume(elem, userId);
        }

        else if (section == 'latesttvrecordings') {
            Sections.loadLatestLiveTvRecordings(elem, userId);
        }

        else if (section == 'folders') {
            Sections.loadLibraryFolders(elem, userId, 'backdrop', index);

        } else if (section == 'latestchannelmedia') {
            Sections.loadLatestChannelMedia(elem, userId);

        } else {

            elem.empty();
        }
    }

    function loadSections(page, userId, displayPreferences) {

        var i, length;
        var sectionCount = 4;

        var elem = $('.sections', page);

        if (!elem.html().length) {
            var html = '';
            for (i = 0, length = sectionCount; i < length; i++) {

                html += '<div class="homePageSection section' + i + '"></div>';
            }

            elem.html(html);
        }

        for (i = 0, length = sectionCount; i < length; i++) {

            loadSection(page, userId, displayPreferences, i);
        }
    }

    var homePageDismissValue = '5';
    var homePageTourKey = 'homePageTour';

    function dismissWelcome(page, userId) {

        ApiClient.getDisplayPreferences('home', userId, 'webclient').done(function (result) {

            result.CustomPrefs[homePageTourKey] = homePageDismissValue;
            ApiClient.updateDisplayPreferences('home', result, userId, 'webclient');
        });
    }

    function showWelcomeIfNeeded(page, displayPreferences) {

        if (displayPreferences.CustomPrefs[homePageTourKey] == homePageDismissValue) {
            $('.welcomeMessage', page).hide();
        } else {

            var elem = $('.welcomeMessage', page).show();

            if (displayPreferences.CustomPrefs[homePageTourKey]) {

                $('.tourHeader', elem).html(Globalize.translate('HeaderWelcomeBack'));
                $('.tourButtonText', elem).html(Globalize.translate('ButtonTakeTheTourToSeeWhatsNew'));

            } else {

                $('.tourHeader', elem).html(Globalize.translate('HeaderWelcomeToMediaBrowserWebClient'));
                $('.tourButtonText', elem).html(Globalize.translate('ButtonTakeTheTour'));
            }
        }
    }

    function takeTour(page, userId) {

        $.swipebox([
                { href: 'css/images/tour/web/tourcontent.jpg', title: Globalize.translate('WebClientTourContent') },
                { href: 'css/images/tour/web/tourmovies.jpg', title: Globalize.translate('WebClientTourMovies') },
                { href: 'css/images/tour/web/tourmouseover.jpg', title: Globalize.translate('WebClientTourMouseOver') },
                { href: 'css/images/tour/web/tourtaphold.jpg', title: Globalize.translate('WebClientTourTapHold') },
                { href: 'css/images/tour/web/tourmysync.png', title: Globalize.translate('WebClientTourMySync') },
                { href: 'css/images/tour/web/toureditor.png', title: Globalize.translate('WebClientTourMetadataManager') },
                { href: 'css/images/tour/web/tourplaylist.png', title: Globalize.translate('WebClientTourPlaylists') },
                { href: 'css/images/tour/web/tourcollections.jpg', title: Globalize.translate('WebClientTourCollections') },
                { href: 'css/images/tour/web/tourusersettings1.png', title: Globalize.translate('WebClientTourUserPreferences1') },
                { href: 'css/images/tour/web/tourusersettings2.png', title: Globalize.translate('WebClientTourUserPreferences2') },
                { href: 'css/images/tour/web/tourusersettings3.png', title: Globalize.translate('WebClientTourUserPreferences3') },
                { href: 'css/images/tour/web/tourusersettings4.png', title: Globalize.translate('WebClientTourUserPreferences4') },
                { href: 'css/images/tour/web/tourmobile1.jpg', title: Globalize.translate('WebClientTourMobile1') },
                { href: 'css/images/tour/web/tourmobile2.png', title: Globalize.translate('WebClientTourMobile2') },
                { href: 'css/images/tour/enjoy.jpg', title: Globalize.translate('MessageEnjoyYourStay') }
        ], {
            afterClose: function () {
                dismissWelcome(page, userId);
                $('.welcomeMessage', page).hide();
            },
            hideBarsDelay: 30000
        });
    }

    $(document).on('pageinit', "#indexPage", function () {

        var page = this;

        var userId = Dashboard.getCurrentUserId();

        $('.btnTakeTour', page).on('click', function () {
            takeTour(page, userId);
        });

    }).on('pagebeforeshow', "#indexPage", function () {

        var page = this;

        var userId = Dashboard.getCurrentUserId();

        ApiClient.getDisplayPreferences('home', userId, 'webclient').done(function (result) {

            showWelcomeIfNeeded(page, result);
            loadSections(page, userId, result);
        });

    });

})(jQuery, document);
