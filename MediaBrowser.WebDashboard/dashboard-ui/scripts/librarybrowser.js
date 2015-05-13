﻿var LibraryBrowser = (function (window, document, $, screen, store) {

    var pageSizeKey = 'pagesize_v4';

    var defaultBackground = "#333";

    return {
        getDefaultPageSize: function (key, defaultValue) {

            var saved = store.getItem(key || pageSizeKey);

            if (saved) {
                return parseInt(saved);
            }

            if (defaultValue) {
                return defaultValue;
            }

            // Chrome seems to have virtualization built-in and can handle large lists easily
            var isChrome = $.browser.chrome;

            return isChrome ? 200 : 100;
        },

        getDefaultItemsView: function (view, mobileView) {

            return $.browser.mobile ? mobileView : view;

        },

        loadSavedQueryValues: function (key, query) {

            var values = store.getItem(key + '_' + Dashboard.getCurrentUserId());

            if (values) {

                values = JSON.parse(values);

                return $.extend(query, values);
            }

            return query;
        },

        saveQueryValues: function (key, query) {

            var values = {};

            if (query.SortBy) {
                values.SortBy = query.SortBy;
            }
            if (query.SortOrder) {
                values.SortOrder = query.SortOrder;
            }

            try {
                store.setItem(key + '_' + Dashboard.getCurrentUserId(), JSON.stringify(values));
            } catch (e) {

            }
        },

        saveViewSetting: function (key, value) {

            try {
                store.setItem(key + '_' + Dashboard.getCurrentUserId() + '_view', value);
            } catch (e) {

            }
        },

        getSavedViewSetting: function (key) {

            var deferred = $.Deferred();
            var val = store.getItem(key + '_' + Dashboard.getCurrentUserId() + '_view');

            deferred.resolveWith(null, [val]);
            return deferred.promise();
        },

        getDateParamValue: function (date) {

            function formatDigit(i) {
                return i < 10 ? "0" + i : i;
            }

            var d = date;

            return "" + d.getFullYear() + formatDigit(d.getMonth() + 1) + formatDigit(d.getDate()) + formatDigit(d.getHours()) + formatDigit(d.getMinutes()) + formatDigit(d.getSeconds());
        },

        playAllFromHere: function (fn, index) {

            fn(index, 100, "MediaSources,Chapters").done(function (result) {

                MediaController.play({
                    items: result.Items
                });
            });
        },

        queueAllFromHere: function (query, index) {

            fn(index, 100, "MediaSources,Chapters").done(function (result) {

                MediaController.queue({
                    items: result.Items
                });
            });
        },

        getItemCountsHtml: function (options, item) {

            var counts = [];

            var childText;

            if (item.Type == 'Playlist') {

                childText = '';

                if (item.CumulativeRunTimeTicks) {

                    var minutes = item.CumulativeRunTimeTicks / 600000000;

                    minutes = minutes || 1;

                    childText += Globalize.translate('ValueMinutes', Math.round(minutes));

                } else {
                    childText += Globalize.translate('ValueMinutes', 0);
                }

                counts.push(childText);

            }
            else if (options.context == "movies") {

                if (item.MovieCount) {

                    childText = item.MovieCount == 1 ?
					Globalize.translate('ValueOneMovie') :
					Globalize.translate('ValueMovieCount', item.MovieCount);

                    counts.push(childText);
                }
                if (item.TrailerCount) {

                    childText = item.TrailerCount == 1 ?
					Globalize.translate('ValueOneTrailer') :
					Globalize.translate('ValueTrailerCount', item.TrailerCount);

                    counts.push(childText);
                }

            } else if (options.context == "tv") {

                if (item.SeriesCount) {

                    childText = item.SeriesCount == 1 ?
					Globalize.translate('ValueOneSeries') :
					Globalize.translate('ValueSeriesCount', item.SeriesCount);

                    counts.push(childText);
                }
                if (item.EpisodeCount) {

                    childText = item.EpisodeCount == 1 ?
					Globalize.translate('ValueOneEpisode') :
					Globalize.translate('ValueEpisodeCount', item.EpisodeCount);

                    counts.push(childText);
                }

            } else if (options.context == "games") {

                if (item.GameCount) {

                    childText = item.GameCount == 1 ?
					Globalize.translate('ValueOneGame') :
					Globalize.translate('ValueGameCount', item.GameCount);

                    counts.push(childText);
                }
            } else if (options.context == "music") {

                if (item.AlbumCount) {

                    childText = item.AlbumCount == 1 ?
					Globalize.translate('ValueOneAlbum') :
					Globalize.translate('ValueAlbumCount', item.AlbumCount);

                    counts.push(childText);
                }
                if (item.SongCount) {

                    childText = item.SongCount == 1 ?
					Globalize.translate('ValueOneSong') :
					Globalize.translate('ValueSongCount', item.SongCount);

                    counts.push(childText);
                }
                if (item.MusicVideoCount) {

                    childText = item.MusicVideoCount == 1 ?
					Globalize.translate('ValueOneMusicVideo') :
					Globalize.translate('ValueMusicVideoCount', item.MusicVideoCount);

                    counts.push(childText);
                }
            }

            return counts.join(' • ');
        },

        getArtistLinksHtml: function (artists, cssClass) {

            var html = [];

            for (var i = 0, length = artists.length; i < length; i++) {

                var artist = artists[i];

                var css = cssClass ? (' class="' + cssClass + '"') : '';
                html.push('<a' + css + ' href="itembynamedetails.html?context=music&id=' + artist.Id + '">' + artist.Name + '</a>');

            }

            html = html.join(' / ');

            return html;
        },

        showPlayMenu: function (positionTo, itemId, itemType, isFolder, mediaType, resumePositionTicks, showAddToPlaylist) {

            var externalPlayers = ExternalPlayer.getExternalPlayers();

            if (!resumePositionTicks && mediaType != "Audio" && !isFolder) {

                if (!externalPlayers.length || mediaType != "Video") {
                    MediaController.play(itemId);
                    return;
                }
            }

            $('.playFlyout').popup("close").remove();

            var html = '<div data-role="popup" class="playFlyout" data-history="false" data-theme="a">';

            html += '<ul data-role="listview" style="min-width: 180px;">';
            html += '<li data-role="list-divider">' + Globalize.translate('HeaderMenu') + '</li>';

            html += '<li><a href="#" onclick="MediaController.play(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">' + Globalize.translate('ButtonPlay') + '</a></li>';

            if (!isFolder && externalPlayers.length) {
                html += '<li><a href="#" onclick="LibraryBrowser.closePlayMenu();ExternalPlayer.showMenu(\'' + itemId + '\');">' + Globalize.translate('ButtonPlayExternalPlayer') + '</a></li>';
            }

            if (resumePositionTicks) {
                html += '<li><a href="#" onclick="MediaController.play({ids:[\'' + itemId + '\'],startPositionTicks:' + resumePositionTicks + '});LibraryBrowser.closePlayMenu();">' + Globalize.translate('ButtonResume') + '</a></li>';
            }

            if (MediaController.canQueueMediaType(mediaType, itemType)) {
                html += '<li><a href="#" onclick="MediaController.queue(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">' + Globalize.translate('ButtonQueue') + '</a></li>';
            }

            if (itemType == "Audio" || itemType == "MusicAlbum" || itemType == "MusicArtist" || itemType == "MusicGenre") {
                html += '<li><a href="#" onclick="MediaController.instantMix(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">' + Globalize.translate('ButtonInstantMix') + '</a></li>';
            }

            if (isFolder || itemType == "MusicArtist" || itemType == "MusicGenre") {
                html += '<li><a href="#" onclick="MediaController.shuffle(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">' + Globalize.translate('ButtonShuffle') + '</a></li>';
            }

            if (showAddToPlaylist) {
                html += '<li><a href="#" onclick="PlaylistManager.showPanel([\'' + itemId + '\']);LibraryBrowser.closePlayMenu();">' + Globalize.translate('ButtonAddToPlaylist') + '</a></li>';
            }

            html += '</ul>';

            html += '</div>';

            $($.mobile.activePage).append(html);

            $('.playFlyout').popup({ positionTo: positionTo || "window" }).trigger('create').popup("open").on("popupafterclose", function () {

                $(this).off("popupafterclose").remove();

            }).parents(".ui-popup-container");
        },

        closePlayMenu: function () {
            $('.playFlyout').popup("close").remove();
        },

        getMoreCommands: function (item, user) {

            var commands = [];

            if (BoxSetEditor.supportsAddingToCollection(item)) {
                commands.push('addtocollection');
            }

            if (PlaylistManager.supportsPlaylists(item)) {
                commands.push('playlist');
            }

            if (item.Type == 'BoxSet' || item.Type == 'Playlist') {
                commands.push('delete');
            }
            else if (item.CanDelete) {
                commands.push('delete');
            }

            if (user.Policy.IsAdministrator) {
                if (item.Type != "Recording" && item.Type != "Program") {
                    commands.push('edit');
                }
            }

            commands.push('refresh');

            if (SyncManager.isAvailable(item, user)) {
                commands.push('sync');
            }

            if (item.CanDownload) {
                commands.push('download');
            }

            return commands;
        },

        refreshItem: function (itemId) {

            ApiClient.refreshItem(itemId, {

                Recursive: true,
                ImageRefreshMode: 'FullRefresh',
                MetadataRefreshMode: 'FullRefresh',
                ReplaceAllImages: false,
                ReplaceAllMetadata: true

            });


            Dashboard.alert(Globalize.translate('MessageRefreshQueued'));
        },

        deleteItem: function (itemId) {

            // The timeout allows the flyout to close
            setTimeout(function () {

                var msg = "<p>" + Globalize.translate('ConfirmDeleteItem') + "</p>";

                Dashboard.confirm(msg, Globalize.translate('HeaderDeleteItem'), function (result) {

                    if (result) {
                        ApiClient.deleteItem(itemId);

                        $(LibraryBrowser).trigger('itemdeleting', [itemId]);
                    }
                });

            }, 250);
        },

        showMoreCommands: function (positionTo, itemId, commands) {

            $('.playFlyout').popup("close").remove();

            var html = '<div data-role="popup" class="playFlyout" data-history="false" data-theme="a">';

            html += '<ul data-role="listview" style="min-width: 180px;">';
            html += '<li data-role="list-divider">' + Globalize.translate('HeaderMenu') + '</li>';

            if (commands.indexOf('addtocollection') != -1) {
                html += '<li data-icon="plus"><a href="#" onclick="$(\'.playFlyout\').popup(\'close\');BoxSetEditor.showPanel([\'' + itemId + '\']);">' + Globalize.translate('ButtonAddToCollection') + '</a></li>';
            }

            if (commands.indexOf('playlist') != -1) {
                html += '<li data-icon="plus"><a href="#" onclick="$(\'.playFlyout\').popup(\'close\');PlaylistManager.showPanel([\'' + itemId + '\']);">' + Globalize.translate('ButtonAddToPlaylist') + '</a></li>';
            }

            if (commands.indexOf('delete') != -1) {
                html += '<li data-icon="delete"><a class="btnMoreMenuDelete" href="#" onclick="$(\'.playFlyout\').popup(\'close\');LibraryBrowser.deleteItem([\'' + itemId + '\']);">' + Globalize.translate('ButtonDelete') + '</a></li>';
            }

            if (commands.indexOf('download') != -1) {
                var downloadHref = ApiClient.getUrl("Items/" + itemId + "/Download", {
                    api_key: ApiClient.accessToken()
                });

                html += '<li data-icon="arrow-d"><a class="btnMoreMenuDownload" data-ajax="false" href="' + downloadHref + '" onclick="$(\'.playFlyout\').popup(\'close\');">' + Globalize.translate('ButtonDownload') + '</a></li>';
            }

            if (commands.indexOf('edit') != -1) {
                html += '<li data-icon="edit"><a href="edititemmetadata.html?id=' + itemId + '">' + Globalize.translate('ButtonEdit') + '</a></li>';
            }

            if (commands.indexOf('refresh') != -1) {
                html += '<li data-icon="refresh"><a class="btnMoreMenuRefresh" href="#">' + Globalize.translate('ButtonRefresh') + '</a></li>';
            }

            html += '</ul>';

            html += '</div>';

            $($.mobile.activePage).append(html);

            var elem = $('.playFlyout').popup({ positionTo: positionTo || "window" }).trigger('create').popup("open").on("popupafterclose", function () {

                $(this).off("popupafterclose").remove();

            });

            $('.btnMoreMenuRefresh', elem).on('click', function () {

                $('.playFlyout').popup('close');

                ApiClient.refreshItem(itemId, {

                    Recursive: true,
                    ImageRefreshMode: 'FullRefresh',
                    MetadataRefreshMode: 'FullRefresh',
                    ReplaceAllImages: false,
                    ReplaceAllMetadata: true
                }).done(function () {

                });
            });
        },

        getHref: function (item, context, topParentId) {

            var href = LibraryBrowser.getHrefInternal(item, context);

            if (context) {
                href += href.indexOf('?') == -1 ? "?context=" : "&context=";
                href += context;
            }

            if (topParentId == null && context != 'playlists') {
                topParentId = LibraryMenu.getTopParentId();
            }

            if (topParentId) {
                href += href.indexOf('?') == -1 ? "?topParentId=" : "&topParentId=";
                href += topParentId;
            }

            return href;
        },

        getHrefInternal: function (item, context) {

            if (!item) {
                throw new Error('item cannot be null');
            }

            if (item.url) {
                return item.url;
            }

            // Handle search hints
            var id = item.Id || item.ItemId;

            if (item.CollectionType == 'livetv') {
                return 'livetvsuggested.html';
            }

            if (item.CollectionType == 'channels') {

                if (AppInfo.enableLatestChannelItems) {
                    return 'channelslatest.html';
                } else {
                    return 'channels.html';
                }
            }

            if (context != 'folders') {
                if (item.CollectionType == 'movies') {
                    return 'moviesrecommended.html?topParentId=' + item.Id;
                }

                if (item.CollectionType == 'boxsets') {
                    return 'collections.html?topParentId=' + item.Id;
                }

                if (item.CollectionType == 'tvshows') {
                    return 'tvrecommended.html?topParentId=' + item.Id;
                }

                if (item.CollectionType == 'music') {
                    return 'musicrecommended.html?topParentId=' + item.Id;
                }

                if (item.CollectionType == 'games') {
                    return 'gamesrecommended.html?topParentId=' + item.Id;
                }
                if (item.CollectionType == 'playlists') {
                    return 'playlists.html?topParentId=' + item.Id;
                }
                if (item.CollectionType == 'photos') {
                    return 'photos.html?topParentId=' + item.Id;
                }
            }
            if (item.Type == 'CollectionFolder') {
                return 'itemlist.html?topParentId=' + item.Id + '&parentid=' + item.Id;
            }

            if (item.Type == "PhotoAlbum" && context == 'photos') {
                return "photos.html?parentId=" + id;
            }
            if (item.Type == "Playlist") {
                return "playlistedit.html?id=" + id;
            }
            if (item.Type == "TvChannel") {
                return "livetvchannel.html?id=" + id;
            }
            if (item.Type == "Channel") {
                return "channelitems.html?id=" + id;
            }
            if (item.Type == "ChannelFolderItem") {
                return "channelitems.html?id=" + item.ChannelId + '&folderId=' + item.Id;
            }
            if (item.Type == "Program") {
                return "livetvprogram.html?id=" + id;
            }
            if (item.Type == "Series") {
                return "itemdetails.html?id=" + id;
            }
            if (item.Type == "Season") {
                return "itemdetails.html?id=" + id;
            }
            if (item.Type == "BoxSet") {
                return "itemdetails.html?id=" + id;
            }
            if (item.Type == "MusicAlbum") {
                return "itemdetails.html?id=" + id;
            }
            if (item.Type == "GameSystem") {
                return "itemdetails.html?id=" + id;
            }
            if (item.Type == "Genre") {
                return "itembynamedetails.html?id=" + id;
            }
            if (item.Type == "MusicGenre") {
                return "itembynamedetails.html?id=" + id;
            }
            if (item.Type == "GameGenre") {
                return "itembynamedetails.html?id=" + id;
            }
            if (item.Type == "Studio") {
                return "itembynamedetails.html?id=" + id;
            }
            if (item.Type == "Person") {
                return "itembynamedetails.html?id=" + id;
            }
            if (item.Type == "Recording") {
                return "livetvrecording.html?id=" + id;
            }

            if (item.Type == "MusicArtist") {
                return "itembynamedetails.html?id=" + id;
            }

            if (item.IsFolder) {
                return id ? "itemlist.html?parentId=" + id : "#";
            }

            return "itemdetails.html?id=" + id;
        },

        getImageUrl: function (item, type, index, options) {

            options = options || {};
            options.type = type;
            options.index = index;

            if (type == 'Backdrop') {
                options.tag = item.BackdropImageTags[index];
            } else if (type == 'Screenshot') {
                options.tag = item.ScreenshotImageTags[index];
            } else if (type == 'Primary') {
                options.tag = item.PrimaryImageTag || item.ImageTags[type];
            } else {
                options.tag = item.ImageTags[type];
            }

            // For search hints
            return ApiClient.getScaledImageUrl(item.Id || item.ItemId, options);

        },

        getListViewIndex: function (item, options) {

            if (options.index == 'disc') {

                return item.ParentIndexNumber == null ? '' : Globalize.translate('ValueDiscNumber', item.ParentIndexNumber);
            }

            var sortBy = (options.sortBy || '').toLowerCase();
            var code, name;

            if (sortBy.indexOf('sortname') == 0) {

                if (item.Type == 'Episode') return '';

                // SortName
                name = (item.SortName || item.Name || '?')[0].toUpperCase();

                code = name.charCodeAt(0);
                if (code < 65 || code > 90) {
                    return '#';
                }

                return name.toUpperCase();
            }
            if (sortBy.indexOf('officialrating') == 0) {

                return item.OfficialRating || Globalize.translate('HeaderUnrated');
            }
            if (sortBy.indexOf('communityrating') == 0) {

                if (item.CommunityRating == null) {
                    return Globalize.translate('HeaderUnrated');
                }

                return Math.floor(item.CommunityRating);
            }
            if (sortBy.indexOf('criticrating') == 0) {

                if (item.CriticRating == null) {
                    return Globalize.translate('HeaderUnrated');
                }

                return Math.floor(item.CriticRating);
            }
            if (sortBy.indexOf('metascore') == 0) {

                if (item.Metascore == null) {
                    return Globalize.translate('HeaderUnrated');
                }

                return Math.floor(item.Metascore);
            }
            if (sortBy.indexOf('albumartist') == 0) {

                // SortName
                if (!item.AlbumArtist) return '';

                name = item.AlbumArtist[0].toUpperCase();

                code = name.charCodeAt(0);
                if (code < 65 || code > 90) {
                    return '#';
                }

                return name.toUpperCase();
            }
            return '';
        },

        getUserDataCssClass: function (key) {

            if (!key) return '';

            return 'libraryItemUserData' + key.replace(new RegExp(' ', 'g'), '');
        },

        getListViewHtml: function (options) {

            var outerHtml = "";

            outerHtml += '<ul data-role="listview" class="itemsListview">';

            if (options.title) {
                outerHtml += '<li data-role="list-divider">';
                outerHtml += options.title;
                outerHtml += '</li>';
            }

            var index = 0;
            var groupTitle = '';

            outerHtml += options.items.map(function (item) {

                var html = '';

                if (options.showIndex !== false) {

                    var itemGroupTitle = LibraryBrowser.getListViewIndex(item, options);

                    if (itemGroupTitle != groupTitle) {

                        html += '<li data-role="list-divider">';
                        html += itemGroupTitle;
                        html += '</li>';

                        groupTitle = itemGroupTitle;
                    }
                }

                var dataAttributes = LibraryBrowser.getItemDataAttributes(item, options, index);

                var cssClass = options.smallIcon ? 'ui-li-has-icon listItem' : 'ui-li-has-thumb listItem';

                if (item.UserData) {
                    cssClass += ' ' + LibraryBrowser.getUserDataCssClass(item.UserData.Key);
                }


                var href = LibraryBrowser.getHref(item, options.context);
                html += '<li class="' + cssClass + '"' + dataAttributes + ' data-itemid="' + item.Id + '" data-playlistitemid="' + (item.PlaylistItemId || '') + '" data-href="' + href + '">';

                var defaultAction = options.defaultAction;
                if (defaultAction == 'play' || defaultAction == 'playallfromhere') {
                    if (item.PlayAccess != 'Full') {
                        defaultAction = null;
                    }
                }
                var defaultActionAttribute = defaultAction ? (' data-action="' + defaultAction + '" class="itemWithAction mediaItem"') : ' class="mediaItem"';
                html += '<a' + defaultActionAttribute + ' href="' + href + '">';

                var imgUrl;

                var downloadWidth = options.smallIcon ? 70 : 80;
                // Scaling 400w episode images to 80 doesn't turn out very well
                var minScale = item.Type == 'Episode' || item.Type == 'Game' || options.smallIcon ? 2 : 1.5;

                if (item.ImageTags.Primary) {

                    imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                        width: downloadWidth,
                        tag: item.ImageTags.Primary,
                        type: "Primary",
                        index: 0,
                        minScale: minScale
                    });

                }
                else if (item.AlbumId && item.AlbumPrimaryImageTag) {

                    imgUrl = ApiClient.getScaledImageUrl(item.AlbumId, {
                        type: "Primary",
                        width: downloadWidth,
                        tag: item.AlbumPrimaryImageTag,
                        minScale: minScale
                    });

                }
                else if (item.AlbumId && item.SeriesPrimaryImageTag) {

                    imgUrl = ApiClient.getScaledImageUrl(item.SeriesId, {
                        type: "Primary",
                        width: downloadWidth,
                        tag: item.SeriesPrimaryImageTag,
                        minScale: minScale
                    });

                }
                else if (item.ParentPrimaryImageTag) {

                    imgUrl = ApiClient.getImageUrl(item.ParentPrimaryImageItemId, {
                        type: "Primary",
                        width: downloadWidth,
                        tag: item.ParentPrimaryImageTag,
                        minScale: minScale
                    });
                }

                if (imgUrl) {
                    var minLazyIndex = 16;
                    if (options.smallIcon) {
                        if (index < minLazyIndex) {
                            html += '<div class="listviewIcon ui-li-icon" style="background-image:url(\'' + imgUrl + '\');"></div>';
                        } else {
                            html += '<div class="listviewIcon ui-li-icon lazy" data-src="' + imgUrl + '"></div>';
                        }
                    } else {
                        if (index < minLazyIndex) {
                            html += '<div class="listviewImage ui-li-thumb" style="background-image:url(\'' + imgUrl + '\');"></div>';
                        } else {
                            html += '<div class="listviewImage ui-li-thumb lazy" data-src="' + imgUrl + '"></div>';
                        }
                    }
                }

                var textlines = [];

                if (item.Type == 'Episode') {
                    textlines.push(item.SeriesName || '&nbsp;');
                } else if (item.Type == 'MusicAlbum') {
                    textlines.push(item.AlbumArtist || '&nbsp;');
                }

                var displayName = LibraryBrowser.getPosterViewDisplayName(item);

                if (options.showIndexNumber && item.IndexNumber != null) {
                    displayName = item.IndexNumber + ". " + displayName;
                }
                textlines.push(displayName);

                if (item.Type == 'Audio') {
                    textlines.push(item.ArtistItems.map(function (a) {
                        return a.Name;

                    }).join(', ') || '&nbsp;');
                }

                if (item.Type == 'Game') {
                    textlines.push(item.GameSystem || '&nbsp;');
                }

                else if (item.Type == 'MusicGenre') {
                    textlines.push('&nbsp;');
                }
                else if (item.Type == 'MusicArtist') {
                    textlines.push('&nbsp;');
                }
                else {
                    textlines.push(LibraryBrowser.getMiscInfoHtml(item));
                }

                html += '<h3>';
                html += textlines[0];
                html += '</h3>';

                if (textlines.length > 1) {
                    html += '<p>';
                    html += textlines[1];
                    html += '</p>';
                }

                html += '<div class="ui-li-aside">';
                html += textlines[2] || LibraryBrowser.getRatingHtml(item, false);
                html += '</div>';

                if (item.Type == 'Series' || item.Type == 'Season' || item.Type == 'BoxSet' || item.MediaType == 'Video') {
                    if (item.UserData.UnplayedItemCount) {
                        //html += '<span class="ui-li-count">' + item.UserData.UnplayedItemCount + '</span>';
                    } else if (item.UserData.Played && item.Type != 'TvChannel') {
                        html += '<div class="playedIndicator"><div class="ui-icon-check ui-btn-icon-notext"></div></div>';
                    }
                }
                html += '</a>';

                // Render out the jqm classes so that we don't have to call trigger create
                html += '<a href="#" data-icon="ellipsis-v" class="listviewMenuButton ui-btn ui-icon-ellipsis-v ui-btn-icon-notext ui-btn-inline">';
                html += '</a>';

                html += '</li>';

                index++;
                return html;

            }).join('');

            outerHtml += '</ul>';

            return outerHtml;
        },

        getItemDataAttributes: function (item, options, index) {

            var atts = [];

            var itemCommands = LibraryBrowser.getItemCommands(item, options);

            atts.push('data-itemid="' + item.Id + '"');
            atts.push('data-commands="' + itemCommands.join(',') + '"');
            atts.push('data-context="' + (options.context || '') + '"');
            atts.push('data-itemtype="' + item.Type + '"');
            atts.push('data-mediatype="' + (item.MediaType || '') + '"');

            if (item.UserData.PlaybackPositionTicks) {
                atts.push('data-positionticks="' + (item.UserData.PlaybackPositionTicks || 0) + '"');
            }

            atts.push('data-playaccess="' + (item.PlayAccess || '') + '"');
            atts.push('data-locationtype="' + (item.LocationType || '') + '"');
            atts.push('data-index="' + index + '"');

            var html = atts.join(' ');

            if (html) {
                html = ' ' + html;
            }

            return html;
        },

        getItemCommands: function (item, options) {

            var itemCommands = [];

            //if (MediaController.canPlay(item)) {
            //    itemCommands.push('playmenu');
            //}

            if (item.Type != "Recording" && item.Type != "Program") {
                itemCommands.push('edit');
            }

            if (item.LocalTrailerCount) {
                itemCommands.push('trailer');
            }

            if (item.MediaType == "Audio" || item.Type == "MusicAlbum" || item.Type == "MusicArtist" || item.Type == "MusicGenre") {
                itemCommands.push('instantmix');
            }

            if (item.IsFolder || item.Type == "MusicArtist" || item.Type == "MusicGenre") {
                itemCommands.push('shuffle');
            }

            if (PlaylistManager.supportsPlaylists(item)) {

                if (options.showRemoveFromPlaylist) {
                    itemCommands.push('removefromplaylist');
                } else {
                    itemCommands.push('playlist');
                }
            }

            if (BoxSetEditor.supportsAddingToCollection(item)) {
                itemCommands.push('addtocollection');
            }

            if (options.playFromHere) {
                itemCommands.push('playfromhere');
                itemCommands.push('queuefromhere');
            }

            // There's no detail page with a dedicated delete function
            if (item.Type == 'Playlist' || item.Type == 'BoxSet') {

                if (item.CanDelete) {
                    itemCommands.push('delete');
                }
            }

            if (SyncManager.isAvailable(item)) {
                itemCommands.push('sync');
            }

            return itemCommands;
        },

        screenWidth: function () {

            var screenWidth = $(window).width();

            return screenWidth;
        },

        getPostersPerRow: function (screenWidth) {

            function getValue(shape) {

                var div = $('<div class="card ' + shape + 'Card"><div class="cardBox"><div class="cardImage"></div></div></div>').appendTo(document.body);
                var width = screenWidth / $('.cardImage', div).innerWidth();
                div.remove();
                return width;
            }

            var info = {};

            info.square = getValue('square');
            info.smallSquare = getValue('smallSquare');
            info.thumb = getValue('backdrop');
            info.portrait = getValue('portrait');
            info.smallPortrait = getValue('smallPortrait');
            info.banner = getValue('banner');
            info.smallThumb = getValue('smallBackdrop');

            return info;
        },

        posterSizes: [],

        getPosterViewInfo: function () {

            var screenWidth = LibraryBrowser.screenWidth();

            var cachedResults = LibraryBrowser.posterSizes;

            for (var i = 0, length = cachedResults.length; i < length; i++) {

                if (cachedResults[i].screenWidth == screenWidth) {
                    return cachedResults[i];
                }
            }

            var result = LibraryBrowser.getPosterViewInfoInternal(screenWidth);

            cachedResults.push(result);

            return result;
        },

        getPosterViewInfoInternal: function (screenWidth) {

            var imagesPerRow = LibraryBrowser.getPostersPerRow(screenWidth);

            if (AppInfo.hasLowImageBandwidth) {
                screenWidth *= .95;
            } else {
                screenWidth *= 1.25;
            }

            var thumbWidth = screenWidth / imagesPerRow.thumb;
            var smallThumbWidth = screenWidth / imagesPerRow.smallThumb;
            var posterWidth = screenWidth / imagesPerRow.portrait;
            var smallPosterWidth = screenWidth / imagesPerRow.smallPortrait;
            var squareSize = screenWidth / imagesPerRow.square;
            var smallSquareSize = screenWidth / imagesPerRow.smallSquare;
            var bannerWidth = screenWidth / imagesPerRow.banner;

            if (!AppInfo.isTouchPreferred) {

                var roundTo = 100;

                thumbWidth = Math.round(thumbWidth / roundTo) * roundTo;
                smallThumbWidth = Math.round(smallThumbWidth / roundTo) * roundTo;
                posterWidth = Math.round(posterWidth / roundTo) * roundTo;
                smallPosterWidth = Math.round(smallPosterWidth / roundTo) * roundTo;
                squareSize = Math.round(squareSize / roundTo) * roundTo;
                bannerWidth = Math.round(bannerWidth / roundTo) * roundTo;
            }

            var defaultPortait = 'portrait';
            var defaultThumb = 'backdrop';
            var defaultSquare = 'square';

            if (AppInfo.hasLowImageBandwidth) {
                defaultThumb = 'smallBackdrop';
                defaultSquare = 'smallSquare';
                defaultPortait = 'smallPortrait';
            }

            return {

                defaultThumb: defaultThumb,
                smallThumbWidth: Math.round(smallThumbWidth),
                thumbWidth: Math.round(thumbWidth),

                defaultPortait: defaultPortait,
                posterWidth: Math.round(posterWidth),
                smallPosterWidth: Math.round(smallPosterWidth),

                defaultSquare: defaultSquare,
                squareSize: Math.round(squareSize),
                smallSquareSize: Math.round(smallSquareSize),

                bannerWidth: Math.round(bannerWidth),
                screenWidth: screenWidth
            };
        },

        getPosterViewHtml: function (options) {

            var items = options.items;
            var currentIndexValue;

            options.shape = options.shape || "portrait";

            var html = "";

            var primaryImageAspectRatio = LibraryBrowser.getAveragePrimaryImageAspectRatio(items);
            var isThumbAspectRatio = primaryImageAspectRatio && Math.abs(primaryImageAspectRatio - 1.777777778) < .3;
            var isSquareAspectRatio = primaryImageAspectRatio && Math.abs(primaryImageAspectRatio - 1) < .33 ||
                primaryImageAspectRatio && Math.abs(primaryImageAspectRatio - 1.3333334) < .01;

            if (options.shape == 'auto' || options.shape == 'autohome') {

                if (isThumbAspectRatio) {
                    options.shape = options.shape == 'auto' ? 'backdrop' : 'backdrop';
                } else if (isSquareAspectRatio) {
                    options.coverImage = true;
                    options.shape = 'square';
                } else if (primaryImageAspectRatio && primaryImageAspectRatio > 1.9) {
                    options.shape = 'banner';
                    options.coverImage = true;
                } else if (primaryImageAspectRatio && Math.abs(primaryImageAspectRatio - 0.6666667) < .2) {
                    options.shape = options.shape == 'auto' ? 'portrait' : 'portrait';
                } else {
                    options.shape = options.defaultShape || (options.shape == 'auto' ? 'portrait' : 'portrait');
                }
            }

            var posterInfo = LibraryBrowser.getPosterViewInfo();

            var thumbWidth = posterInfo.thumbWidth;
            var posterWidth = posterInfo.posterWidth;
            var squareSize = posterInfo.squareSize;
            var bannerWidth = posterInfo.bannerWidth;

            if (isThumbAspectRatio) {
                posterInfo.smallPosterWidth = posterInfo.smallThumbWidth;
                posterWidth = thumbWidth;
            }
            else if (isSquareAspectRatio) {
                posterInfo.smallPosterWidth = posterInfo.smallSquareSize;
                posterWidth = squareSize;
            }

            if (options.shape == 'backdrop' && posterInfo.defaultThumb == 'smallBackdrop') {
                options.shape = 'smallBackdrop';
            }

            else if (options.shape == 'portrait' && posterInfo.defaultPortait == 'smallPortrait') {
                options.shape = 'smallPortrait';
            }

            else if (options.shape == 'square' && posterInfo.defaultSquare == 'smallSquare') {
                options.shape = 'smallSquare';
            }

            if (options.shape == 'smallBackdrop') {
                thumbWidth = posterInfo.smallThumbWidth;
            }
            else if (options.shape == 'smallPortrait') {
                posterWidth = posterInfo.smallPosterWidth;
            }
            else if (options.shape == 'smallSquare') {
                squareSize = posterInfo.smallSquareSize;
            }
            else if (options.shape == 'detailPagePortrait') {
                posterWidth = 200;
            }
            else if (options.shape == 'detailPage169') {
                posterWidth = 320;
                thumbWidth = 320;
            }

            var dateText;

            for (var i = 0, length = items.length; i < length; i++) {

                var item = items[i];

                dateText = null;

                primaryImageAspectRatio = LibraryBrowser.getAveragePrimaryImageAspectRatio([item]);

                if (options.showPremiereDateIndex) {

                    if (item.PremiereDate) {
                        try {

                            dateText = LibraryBrowser.getFutureDateText(parseISO8601Date(item.PremiereDate, { toLocal: true }), true);

                        } catch (err) {
                        }
                    }

                    var newIndexValue = dateText || Globalize.translate('HeaderUnknownDate');

                    if (newIndexValue != currentIndexValue) {

                        html += '<h2 class="timelineHeader detailSectionHeader" style="text-align:center;">' + newIndexValue + '</h2>';
                        currentIndexValue = newIndexValue;
                    }
                }
                else if (options.showStartDateIndex) {

                    if (item.StartDate) {
                        try {

                            dateText = LibraryBrowser.getFutureDateText(parseISO8601Date(item.StartDate, { toLocal: true }), true);

                        } catch (err) {
                        }
                    }

                    var newIndexValue = dateText || Globalize.translate('HeaderUnknownDate');

                    if (newIndexValue != currentIndexValue) {

                        html += '<h2 class="timelineHeader detailSectionHeader" style="text-align:center;">' + newIndexValue + '</h2>';
                        currentIndexValue = newIndexValue;
                    }
                } else if (options.timeline) {
                    var year = item.ProductionYear || Globalize.translate('HeaderUnknownYear');

                    if (year != currentIndexValue) {

                        html += '<h2 class="timelineHeader detailSectionHeader">' + year + '</h2>';
                        currentIndexValue = year;
                    }
                }

                html += LibraryBrowser.getPosterViewItemHtml(item, i, options, primaryImageAspectRatio, thumbWidth, posterWidth, squareSize, bannerWidth);
            }

            return html;
        },

        getPosterViewItemHtml: function (item, index, options, primaryImageAspectRatio, thumbWidth, posterWidth, squareSize, bannerWidth) {

            var html = '';
            var imgUrl = null;
            var icon;
            var background = null;
            var width = null;
            var height = null;

            var forceName = false;

            var enableImageEnhancers = options.enableImageEnhancers !== false;

            if (options.autoThumb && item.ImageTags && item.ImageTags.Primary && item.PrimaryImageAspectRatio && item.PrimaryImageAspectRatio >= 1.5) {

                width = posterWidth;
                height = primaryImageAspectRatio ? Math.round(posterWidth / primaryImageAspectRatio) : null;

                imgUrl = ApiClient.getImageUrl(item.Id, {
                    type: "Primary",
                    height: height,
                    width: width,
                    tag: item.ImageTags.Primary,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (options.autoThumb && item.ImageTags && item.ImageTags.Thumb) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    tag: item.ImageTags.Thumb,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (options.preferBackdrop && item.BackdropImageTags && item.BackdropImageTags.length) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Backdrop",
                    maxWidth: thumbWidth,
                    tag: item.BackdropImageTags[0],
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (options.preferThumb && item.ImageTags && item.ImageTags.Thumb) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    tag: item.ImageTags.Thumb,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (options.preferBanner && item.ImageTags && item.ImageTags.Banner) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Banner",
                    maxWidth: bannerWidth,
                    tag: item.ImageTags.Banner,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (options.preferThumb && item.SeriesThumbImageTag && options.inheritThumb !== false) {

                imgUrl = ApiClient.getScaledImageUrl(item.SeriesId, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    tag: item.SeriesThumbImageTag,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (options.preferThumb && item.ParentThumbItemId && options.inheritThumb !== false) {

                imgUrl = ApiClient.getThumbImageUrl(item.ParentThumbItemId, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (options.preferThumb && item.BackdropImageTags && item.BackdropImageTags.length) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Backdrop",
                    maxWidth: thumbWidth,
                    tag: item.BackdropImageTags[0],
                    enableImageEnhancers: enableImageEnhancers
                });

                forceName = true;

            } else if (item.ImageTags && item.ImageTags.Primary) {

                width = posterWidth;
                height = primaryImageAspectRatio ? Math.round(posterWidth / primaryImageAspectRatio) : null;

                imgUrl = ApiClient.getImageUrl(item.Id, {
                    type: "Primary",
                    height: height,
                    width: width,
                    tag: item.ImageTags.Primary,
                    enableImageEnhancers: enableImageEnhancers
                });

            }
            else if (item.ParentPrimaryImageTag) {

                imgUrl = ApiClient.getImageUrl(item.ParentPrimaryImageItemId, {
                    type: "Primary",
                    width: posterWidth,
                    tag: item.ParentPrimaryImageTag,
                    enableImageEnhancers: enableImageEnhancers
                });
            }
            else if (item.AlbumId && item.AlbumPrimaryImageTag) {

                height = squareSize;
                width = primaryImageAspectRatio ? Math.round(height * primaryImageAspectRatio) : null;

                imgUrl = ApiClient.getScaledImageUrl(item.AlbumId, {
                    type: "Primary",
                    height: height,
                    width: width,
                    tag: item.AlbumPrimaryImageTag,
                    enableImageEnhancers: enableImageEnhancers
                });

            }
            else if (item.Type == 'Season' && item.ImageTags && item.ImageTags.Thumb) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    tag: item.ImageTags.Thumb,
                    enableImageEnhancers: enableImageEnhancers
                });

            }
            else if (item.BackdropImageTags && item.BackdropImageTags.length) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Backdrop",
                    maxWidth: thumbWidth,
                    tag: item.BackdropImageTags[0],
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (item.ImageTags && item.ImageTags.Thumb) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    tag: item.ImageTags.Thumb,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (item.SeriesThumbImageTag) {

                imgUrl = ApiClient.getScaledImageUrl(item.SeriesId, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    tag: item.SeriesThumbImageTag,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (item.ParentThumbItemId) {

                imgUrl = ApiClient.getThumbImageUrl(item, {
                    type: "Thumb",
                    maxWidth: thumbWidth,
                    enableImageEnhancers: enableImageEnhancers
                });

            } else if (item.MediaType == "Audio" || item.Type == "MusicAlbum" || item.Type == "MusicArtist") {

                if (item.Name && options.showTitle) {
                    icon = 'fa-music';
                }
                background = defaultBackground;

            } else if (item.Type == "Recording" || item.Type == "Program" || item.Type == "TvChannel") {

                if (item.Name && options.showTitle) {
                    icon = 'fa-folder-open';
                }

                background = defaultBackground;
            } else if (item.MediaType == "Video" || item.Type == "Season" || item.Type == "Series") {

                if (item.Name && options.showTitle) {
                    icon = 'fa-video-camera';
                }
                background = defaultBackground;
            } else if (item.Type == "Person") {

                if (item.Name && options.showTitle) {
                    icon = 'fa-user';
                }
                background = defaultBackground;
            } else {
                if (item.Name && options.showTitle) {
                    icon = 'fa-folder-open';
                }
                background = defaultBackground;
            }

            var cssClass = "card";

            if (options.transparent !== false) {
                cssClass += " transparentCard";
            }

            cssClass += ' ' + options.shape + 'Card';

            var mediaSourceCount = item.MediaSourceCount || 1;

            var href = options.linkItem === false ? '#' : LibraryBrowser.getHref(item, options.context);

            if (item.UserData) {
                cssClass += ' ' + LibraryBrowser.getUserDataCssClass(item.UserData.Key);
            }

            if (options.showChildCountIndicator && item.ChildCount && options.showLatestItemsPopup !== false) {
                cssClass += ' groupedCard';
            }

            if (options.showTitle && !options.overlayText) {
                cssClass += ' bottomPaddedCard';
            }

            var dataAttributes = LibraryBrowser.getItemDataAttributes(item, options, index);

            var defaultAction = options.defaultAction;
            if (defaultAction == 'play' || defaultAction == 'playallfromhere') {
                if (item.PlayAccess != 'Full') {
                    defaultAction = null;
                }
            }
            var defaultActionAttribute = defaultAction ? (' data-action="' + defaultAction + '"') : '';

            // card
            html += '<div' + dataAttributes + ' class="' + cssClass + '">';

            var style = "";

            if (imgUrl && !options.lazy) {
                style += 'background-image:url(\'' + imgUrl + '\');';
            }

            if (background) {
                style += "background-color:" + background + ";";
            }

            var imageCssClass = 'cardImage';

            if (icon) {
                imageCssClass += " iconCardImage";
            }
            if (options.coverImage) {
                imageCssClass += " coveredCardImage";
            }
            if (options.centerImage) {
                imageCssClass += " centeredCardImage";
            }

            var dataSrc = "";

            if (options.lazy && imgUrl) {
                imageCssClass += " lazy";
                dataSrc = ' data-src="' + imgUrl + '"';
            }

            var cardboxCssClass = 'cardBox';

            if (options.cardLayout) {
                cardboxCssClass += ' visualCardBox';
            }
            html += '<div class="' + cardboxCssClass + '">';
            html += '<div class="cardScalable">';

            html += '<div class="cardPadder"></div>';

            var anchorCssClass = "cardContent";

            anchorCssClass += ' mediaItem';

            if (options.defaultAction) {
                anchorCssClass += ' itemWithAction';
            }

            html += '<a class="' + anchorCssClass + '" href="' + href + '"' + defaultActionAttribute + '>';
            html += '<div class="' + imageCssClass + '" style="' + style + '"' + dataSrc + '>';
            if (icon) {
                html += '<i class="fa ' + icon + '"></i>';
            }
            html += '</div>';

            html += '<div class="cardOverlayTarget"></div>';

            if (item.LocationType == "Offline" || item.LocationType == "Virtual") {
                if (options.showLocationTypeIndicator !== false) {
                    html += LibraryBrowser.getOfflineIndicatorHtml(item);
                }
            } else if (options.showUnplayedIndicator !== false) {
                html += LibraryBrowser.getPlayedIndicatorHtml(item);
            } else if (options.showChildCountIndicator) {
                html += LibraryBrowser.getGroupCountIndicator(item);
            }

            if (mediaSourceCount > 1) {
                html += '<div class="mediaSourceIndicator">' + mediaSourceCount + '</div>';
            }
            if (item.IsUnidentified) {
                html += '<div class="unidentifiedIndicator"><div class="ui-icon-alert ui-btn-icon-notext"></div></div>';
            }

            var progressHtml = options.showProgress === false || item.IsFolder ? '' : LibraryBrowser.getItemProgressBarHtml((item.Type == 'Recording' ? item : item.UserData));

            var footerOverlayed = false;

            if (options.overlayText || (forceName && !options.showTitle)) {

                var footerCssClass = progressHtml ? 'cardFooter fullCardFooter' : 'cardFooter';

                html += LibraryBrowser.getCardFooterText(item, options, imgUrl, forceName, footerCssClass, progressHtml);
                footerOverlayed = true;
            }
            else if (progressHtml) {
                html += '<div class="cardFooter fullCardFooter lightCardFooter">';
                html += "<div class='cardProgress cardText'>";
                html += progressHtml;
                html += "</div>";
                //cardFooter
                html += "</div>";

                progressHtml = '';
            }

            // cardContent
            html += '</a>';

            // cardScalable
            html += '</div>';

            if (!options.overlayText && !footerOverlayed) {
                html += LibraryBrowser.getCardFooterText(item, options, imgUrl, forceName, 'cardFooter outerCardFooter', progressHtml);
            }

            // cardBox
            html += '</div>';

            // card
            html += "</div>";

            return html;
        },

        getCardFooterText: function (item, options, imgUrl, forceName, footerClass, progressHtml) {

            var html = '';

            html += '<div class="' + footerClass + '">';

            if (options.cardLayout) {
                html += '<div class="cardText" style="text-align:right; float:right;">';
                // Render out the jqm classes so that we don't have to call trigger create
                html += '<button class="listviewMenuButton ui-btn ui-icon-ellipsis-v ui-btn-icon-notext ui-btn-inline ui-shadow ui-corner-all" type="button" data-inline="true" data-iconpos="notext" data-icon="ellipsis-v" style="margin: 4px 0 0;"></button>';
                html += "</div>";
            }

            var name = LibraryBrowser.getPosterViewDisplayName(item, options.displayAsSpecial);

            if (!imgUrl && !options.showTitle) {
                html += "<div class='cardDefaultText'>";
                html += htmlEncode(name);
                html += "</div>";
            }

            var cssClass = options.centerText ? "cardText cardTextCentered" : "cardText";

            var lines = [];

            if (options.showParentTitle) {

                lines.push(item.EpisodeTitle ? item.Name : (item.SeriesName || item.Album || item.AlbumArtist || item.GameSystem || ""));
            }

            if (options.showTitle || forceName) {

                lines.push(htmlEncode(name));
            }

            if (options.showItemCounts) {

                var itemCountHtml = LibraryBrowser.getItemCountsHtml(options, item);

                lines.push(itemCountHtml);
            }

            if (options.textLines) {
                var additionalLines = options.textLines(item);
                for (var i = 0, length = additionalLines.length; i < length; i++) {
                    lines.push(additionalLines[i]);
                }
            }

            if (options.showSongCount) {

                var songLine = '';

                if (item.SongCount) {
                    songLine = item.SongCount == 1 ?
					Globalize.translate('ValueOneSong') :
					Globalize.translate('ValueSongCount', item.SongCount);
                }

                lines.push(songLine);
            }

            if (options.showPremiereDate && item.PremiereDate) {

                try {

                    lines.push(LibraryBrowser.getPremiereDateText(item));

                } catch (err) {
                    lines.push('');

                }
            }

            if (options.showYear) {

                lines.push(item.ProductionYear || '');
            }

            if (options.showSeriesYear) {

                if (item.Status == "Continuing") {

                    lines.push(Globalize.translate('ValueSeriesYearToPresent', item.ProductionYear || ''));

                } else {
                    lines.push(item.ProductionYear || '');
                }

            }

            if (options.showProgramAirInfo) {

                var date = parseISO8601Date(item.StartDate, { toLocal: true });

                var text = item.StartDate ?
                    date.toLocaleString() :
                    '';

                lines.push(text || '&nbsp;');

                lines.push(item.ChannelName || '&nbsp;');
            }

            html += LibraryBrowser.getCardTextLines(lines, cssClass, !options.overlayText);

            if (options.overlayText) {

                if (progressHtml) {
                    html += "<div class='cardText cardProgress'>";
                    html += progressHtml;
                    html += "</div>";
                }
            }

            //cardFooter
            html += "</div>";

            return html;
        },

        getListItemInfo: function (elem) {
            
            var elemWithAttributes = elem;

            while (!elemWithAttributes.getAttribute('data-itemid')) {
                elemWithAttributes = elemWithAttributes.parentNode;
            }

            var itemId = elemWithAttributes.getAttribute('data-itemid');
            var index = elemWithAttributes.getAttribute('data-index');
            var mediaType = elemWithAttributes.getAttribute('data-mediatype');

            return {
                id: itemId,
                index: index,
                mediaType: mediaType
            };
        },

        getCardTextLines: function (lines, cssClass, forceLines) {

            var html = '';

            var valid = 0;
            var i, length;

            for (i = 0, length = lines.length; i < length; i++) {

                var text = lines[i];

                if (text) {
                    html += "<div class='" + cssClass + "'>";
                    html += text;
                    html += "</div>";
                    valid++;
                }
            }

            if (forceLines) {
                while (valid < length) {
                    html += "<div class='" + cssClass + "'>&nbsp;</div>";
                    valid++;
                }
            }

            return html;
        },

        getFutureDateText: function (date) {

            var weekday = [];
            weekday[0] = Globalize.translate('OptionSunday');
            weekday[1] = Globalize.translate('OptionMonday');
            weekday[2] = Globalize.translate('OptionTuesday');
            weekday[3] = Globalize.translate('OptionWednesday');
            weekday[4] = Globalize.translate('OptionThursday');
            weekday[5] = Globalize.translate('OptionFriday');
            weekday[6] = Globalize.translate('OptionSaturday');

            var day = weekday[date.getDay()];
            date = date.toLocaleDateString();

            if (date.toLowerCase().indexOf(day.toLowerCase()) == -1) {
                return day + " " + date;
            }

            return date;
        },

        getPremiereDateText: function (item, date) {

            if (!date) {

                var text = '';

                if (item.AirTime) {
                    text += item.AirTime;
                }

                if (item.SeriesStudio) {

                    if (text) {
                        text += " on " + item.SeriesStudio;
                    } else {
                        text += item.SeriesStudio;
                    }
                }

                return text;
            }

            var day = LibraryBrowser.getFutureDateText(date);

            if (item.AirTime) {
                day += " at " + item.AirTime;
            }

            if (item.SeriesStudio) {
                day += " on " + item.SeriesStudio;
            }

            return day;
        },

        getPosterViewDisplayName: function (item, displayAsSpecial, includeParentInfo) {

            if (!item) {
                throw new Error("null item passed into getPosterViewDisplayName");
            }

            var name = item.EpisodeTitle || item.Name;

            if (item.Type == "TvChannel") {

                if (item.Number) {
                    return item.Number + ' ' + name;
                }
                return name;
            }
            if (displayAsSpecial && item.Type == "Episode" && item.ParentIndexNumber == 0) {

                name = Globalize.translate('ValueSpecialEpisodeName', name);

            } else if (item.Type == "Episode" && item.IndexNumber != null && item.ParentIndexNumber != null) {

                var displayIndexNumber = item.IndexNumber;

                var number = "E" + displayIndexNumber;

                if (includeParentInfo !== false) {
                    number = "S" + item.ParentIndexNumber + ", " + number;
                }

                if (item.IndexNumberEnd) {

                    displayIndexNumber = item.IndexNumberEnd;
                    number += "-" + displayIndexNumber;
                }

                name = number + " - " + name;

            }

            return name;
        },

        getOfflineIndicatorHtml: function (item) {

            if (item.LocationType == "Offline") {
                return '<div class="posterRibbon offlinePosterRibbon">' + Globalize.translate('HeaderOffline') + '</div>';
            }

            try {

                var date = parseISO8601Date(item.PremiereDate, { toLocal: true });

                if (item.PremiereDate && (new Date().getTime() < date.getTime())) {
                    return '<div class="posterRibbon unairedPosterRibbon">' + Globalize.translate('HeaderUnaired') + '</div>';
                }
            } catch (err) {

            }

            if (item.IsFolder) {
                return '';
            }
            return '<div class="posterRibbon missingPosterRibbon">' + Globalize.translate('HeaderMissing') + '</div>';
        },

        getPlayedIndicatorHtml: function (item) {

            if (item.Type == "Series" || item.Type == "Season" || item.Type == "BoxSet" || item.MediaType == "Video" || item.MediaType == "Game" || item.MediaType == "Book") {
                if (item.UserData.UnplayedItemCount) {
                    return '<div class="playedIndicator">' + item.UserData.UnplayedItemCount + '</div>';
                }

                if (item.Type != 'TvChannel') {
                    if (item.UserData.PlayedPercentage && item.UserData.PlayedPercentage >= 100 || (item.UserData && item.UserData.Played)) {
                        return '<div class="playedIndicator"><div class="ui-icon-check ui-btn-icon-notext"></div></div>';
                    }
                }
            }

            return '';
        },

        getGroupCountIndicator: function (item) {

            if (item.ChildCount) {
                return '<div class="playedIndicator">' + item.ChildCount + '</div>';
            }

            return '';
        },

        getAveragePrimaryImageAspectRatio: function (items) {

            var values = [];

            for (var i = 0, length = items.length; i < length; i++) {

                var ratio = items[i].PrimaryImageAspectRatio || 0;

                if (!ratio) {
                    continue;
                }

                values[values.length] = ratio;
            }

            if (!values.length) {
                return null;
            }

            // Use the median
            values.sort(function (a, b) { return a - b; });

            var half = Math.floor(values.length / 2);

            var result;

            if (values.length % 2)
                result = values[half];
            else
                result = (values[half - 1] + values[half]) / 2.0;

            // If really close to 2:3 (poster image), just return 2:3
            if (Math.abs(0.66666666667 - result) <= .15) {
                return 0.66666666667;
            }

            // If really close to 16:9 (episode image), just return 16:9
            if (Math.abs(1.777777778 - result) <= .2) {
                return 1.777777778;
            }

            // If really close to 1 (square image), just return 1
            if (Math.abs(1 - result) <= .15) {
                return 1;
            }

            // If really close to 4:3 (poster image), just return 2:3
            if (Math.abs(1.33333333333 - result) <= .15) {
                return 1.33333333333;
            }

            return result;
        },

        metroColors: ["#6FBD45", "#4BB3DD", "#4164A5", "#E12026", "#800080", "#E1B222", "#008040", "#0094FF", "#FF00C7", "#FF870F", "#7F0037"],

        getRandomMetroColor: function () {

            var index = Math.floor(Math.random() * (LibraryBrowser.metroColors.length - 1));

            return LibraryBrowser.metroColors[index];
        },

        getMetroColor: function (str) {

            if (str) {
                var character = String(str.substr(0, 1).charCodeAt());
                var sum = 0;
                for (var i = 0; i < character.length; i++) {
                    sum += parseInt(character.charAt(i));
                }
                var index = String(sum).substr(-1);

                return LibraryBrowser.metroColors[index];
            } else {
                return LibraryBrowser.getRandomMetroColor();
            }

        },

        renderName: function (item, nameElem, linkToElement, context) {

            var name = LibraryBrowser.getPosterViewDisplayName(item, false, false);

            Dashboard.setPageTitle(name);

            if (linkToElement) {
                nameElem.html('<a class="detailPageParentLink" href="' + LibraryBrowser.getHref(item, context) + '">' + name + '</a>').trigger('create');
            } else {
                nameElem.html(name);
            }
        },

        renderParentName: function (item, parentNameElem, context) {

            var html = [];

            var contextParam = context ? ('&context=' + context) : '';

            if (item.AlbumArtists) {
                html.push(LibraryBrowser.getArtistLinksHtml(item.AlbumArtists, "detailPageParentLink"));
            } else if (item.ArtistItems && item.ArtistItems.length && item.Type == "MusicVideo") {
                html.push(LibraryBrowser.getArtistLinksHtml(item.ArtistItems, "detailPageParentLink"));
            } else if (item.SeriesName && item.Type == "Episode") {

                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.SeriesId + contextParam + '">' + item.SeriesName + '</a>');
            }

            if (item.SeriesName && item.Type == "Season") {

                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.SeriesId + contextParam + '">' + item.SeriesName + '</a>');

            } else if (item.ParentIndexNumber != null && item.Type == "Episode") {

                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.SeasonId + contextParam + '">' + item.SeasonName + '</a>');

            } else if (item.Album && item.Type == "Audio" && (item.AlbumId || item.ParentId)) {
                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + (item.AlbumId || item.ParentId) + contextParam + '">' + item.Album + '</a>');

            } else if (item.Album && item.Type == "MusicVideo" && item.AlbumId) {
                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.AlbumId + contextParam + '">' + item.Album + '</a>');

            } else if (item.Album) {
                html.push(item.Album);
            }

            if (html.length) {
                parentNameElem.show().html(html.join(' - ')).trigger('create');
            } else {
                parentNameElem.hide();
            }
        },

        renderLinks: function (linksElem, item) {

            var links = [];

            if (item.HomePageUrl) {
                links.push('<a class="textlink" href="' + item.HomePageUrl + '" target="_blank">' + Globalize.translate('ButtonWebsite') + '</a>');
            }

            if (item.ExternalUrls) {

                for (var i = 0, length = item.ExternalUrls.length; i < length; i++) {

                    var url = item.ExternalUrls[i];

                    links.push('<a class="textlink" href="' + url.Url + '" target="_blank">' + url.Name + '</a>');
                }
            }

            if (links.length) {

                var html = links.join('&nbsp;&nbsp;/&nbsp;&nbsp;');

                html = Globalize.translate('ValueLinks', html);

                $(linksElem).html(html).trigger('create');

            } else {
                $(linksElem).hide();
            }
        },

        getDefaultPageSizeSelections: function () {

            return [20, 50, 100, 200, 300, 400, 500];
        },

        getQueryPagingHtml: function (options) {

            var startIndex = options.startIndex;
            var limit = options.limit;
            var totalRecordCount = options.totalRecordCount;

            if (limit && options.updatePageSizeSetting !== false) {
                try {
                    store.setItem(options.pageSizeKey || pageSizeKey, limit);
                } catch (e) {

                }
            }

            var html = '';

            var recordsEnd = Math.min(startIndex + limit, totalRecordCount);

            // 20 is the minimum page size
            var showControls = totalRecordCount > 20 || limit < totalRecordCount;

            html += '<div class="listPaging">';

            html += '<span style="margin-right: 10px;vertical-align:middle;">';

            var startAtDisplay = totalRecordCount ? startIndex + 1 : 0;
            html += startAtDisplay + '-' + recordsEnd + ' of ' + totalRecordCount;

            html += '</span>';

            if (showControls || options.viewButton || options.addSelectionButton || options.additionalButtonsHtml) {

                html += '<div data-role="controlgroup" data-type="horizontal" style="display:inline-block;">';

                if (showControls) {

                    html += '<button data-icon="arrow-l" data-iconpos="notext" data-inline="true" data-mini="true" class="btnPreviousPage" ' + (startIndex ? '' : 'disabled') + '>' + Globalize.translate('ButtonPreviousPage') + '</button>';

                    html += '<button data-icon="arrow-r" data-iconpos="notext" data-inline="true" data-mini="true" class="btnNextPage" ' + (startIndex + limit >= totalRecordCount ? 'disabled' : '') + '>' + Globalize.translate('ButtonNextPage') + '</button>';
                }

                html += (options.additionalButtonsHtml || '');

                if (options.addSelectionButton) {
                    html += '<button data-mini="true" data-icon="check" data-inline="true" data-iconpos="notext" title="' + Globalize.translate('ButtonSelect') + '" class="btnToggleSelections">' + Globalize.translate('ButtonSelect') + '</button>';
                }

                if (options.viewButton) {

                    html += '<button data-icon="ellipsis-v" data-iconpos="notext" data-inline="true" data-mini="true" onclick="$(\'.viewPanel\', $(this).parents(\'.page\')).panel(\'toggle\');">' + Globalize.translate('ButtonView') + '</button>';
                }

                html += '</div>';

                if (showControls && options.showLimit !== false) {
                    var id = "selectPageSize";

                    var pageSizes = options.pageSizes || LibraryBrowser.getDefaultPageSizeSelections();

                    var optionsHtml = pageSizes.map(function (val) {

                        if (limit == val) {

                            return '<option value="' + val + '" selected="selected">' + val + '</option>';

                        } else {
                            return '<option value="' + val + '">' + val + '</option>';
                        }
                    }).join('');

                    // Add styles to defeat jquery mobile
                    html += '<div class="pageSizeContainer"><label style="font-size:inherit;" class="labelPageSize" for="' + id + '">' + Globalize.translate('LabelLimit') + '</label><select class="selectPageSize" id="' + id + '" data-inline="true" data-mini="true">' + optionsHtml + '</select></div>';
                }
            }

            html += '</div>';

            return html;
        },

        getPagingHtml: function (query, totalRecordCount, updatePageSizeSetting, pageSizes, showLimit) {

            if (query.Limit && updatePageSizeSetting !== false) {
                try {
                    store.setItem(pageSizeKey, query.Limit);
                } catch (e) {

                }
            }

            var html = '';

            var recordsEnd = Math.min(query.StartIndex + query.Limit, totalRecordCount);

            // 20 is the minimum page size
            var showControls = totalRecordCount > 20 || query.Limit < totalRecordCount;

            html += '<div class="listPaging">';

            html += '<span style="margin-right: 10px;vertical-align:middle;">';

            var startAtDisplay = totalRecordCount ? query.StartIndex + 1 : 0;
            html += startAtDisplay + '-' + recordsEnd + ' of ' + totalRecordCount;

            html += '</span>';

            if (showControls) {

                html += '<div data-role="controlgroup" data-type="horizontal" style="display:inline-block;">';
                html += '<button type="button" data-icon="arrow-l" data-iconpos="notext" data-inline="true" data-mini="true" class="btnPreviousPage" ' + (query.StartIndex ? '' : 'disabled') + '>' + Globalize.translate('ButtonPreviousPage') + '</button>';

                html += '<button type="button" data-icon="arrow-r" data-iconpos="notext" data-inline="true" data-mini="true" class="btnNextPage" ' + (query.StartIndex + query.Limit >= totalRecordCount ? 'disabled' : '') + '>' + Globalize.translate('ButtonNextPage') + '</button>';
                html += '</div>';

                if (showLimit !== false) {
                    var id = "selectPageSize" + new Date().getTime();

                    var options = '';

                    function getOption(val) {

                        if (query.Limit == val) {

                            return '<option value="' + val + '" selected="selected">' + val + '</option>';

                        } else {
                            return '<option value="' + val + '">' + val + '</option>';
                        }
                    }

                    pageSizes = pageSizes || [20, 50, 100, 200, 300, 400, 500];

                    for (var j = 0, length = pageSizes.length; j < length; j++) {
                        options += getOption(pageSizes[j]);
                    }

                    // Add styles to defeat jquery mobile
                    html += '<div class="pageSizeContainer"><label style="font-size:inherit;" class="labelPageSize" for="' + id + '">' + Globalize.translate('LabelLimit') + '</label><select class="selectPageSize" id="' + id + '" data-inline="true" data-mini="true">' + options + '</select></div>';
                }
            }

            html += '</div>';

            return html;
        },

        getRatingHtml: function (item, metascore) {

            var html = "";

            if (item.CommunityRating) {

                html += "<div class='starRating' title='" + item.CommunityRating + "'></div>";
                html += '<div class="starRatingValue">';
                html += item.CommunityRating.toFixed(1);
                html += '</div>';
            }

            if (item.CriticRating != null) {

                if (item.CriticRating >= 60) {
                    html += '<div class="fresh rottentomatoesicon" title="Rotten Tomatoes"></div>';
                } else {
                    html += '<div class="rotten rottentomatoesicon" title="Rotten Tomatoes"></div>';
                }

                html += '<div class="criticRating" title="Rotten Tomatoes">' + item.CriticRating + '%</div>';
            }

            if (item.Metascore && metascore !== false) {

                if (item.Metascore >= 60) {
                    html += '<div class="metascore metascorehigh" title="Metascore">' + item.Metascore + '</div>';
                }
                else if (item.Metascore >= 40) {
                    html += '<div class="metascore metascoremid" title="Metascore">' + item.Metascore + '</div>';
                } else {
                    html += '<div class="metascore metascorelow" title="Metascore">' + item.Metascore + '</div>';
                }
            }

            return html;
        },

        getItemProgressBarHtml: function (item) {


            if (item.Type == "Recording" && item.CompletionPercentage) {

                return '<progress class="itemProgressBar recordingProgressBar" min="0" max="100" value="' + item.CompletionPercentage + '"></progress>';
            }

            var pct = item.PlayedPercentage;

            if (pct && pct < 100) {

                return '<progress class="itemProgressBar" min="0" max="100" value="' + pct + '"></progress>';
            }

            return null;
        },

        getUserDataButtonHtml: function (method, itemId, btnCssClass, icon, tooltip) {

            btnCssClass += " imageButton";

            return '<button data-itemid="' + itemId + '" class="' + btnCssClass + '" type="button" onclick="LibraryBrowser.' + method + '(this);return false;" title="' + tooltip + '"><div class="fa ' + icon + '"></div></button>';

        },

        getUserDataIconsHtml: function (item) {

            var html = '';

            var userData = item.UserData || {};

            var itemId = item.Id;
            var type = item.Type;

            var tooltipPlayed = Globalize.translate('TooltipPlayed');

            if ((item.MediaType || item.IsFolder) && type != "TvChannel" && type != "MusicArtist") {
                if (userData.Played) {
                    html += LibraryBrowser.getUserDataButtonHtml('markPlayed', itemId, 'btnUserItemRating btnUserItemRating', 'fa-check', tooltipPlayed);
                } else {
                    html += LibraryBrowser.getUserDataButtonHtml('markPlayed', itemId, 'btnUserItemRating btnUserItemRatingOff', 'fa-check', tooltipPlayed);
                }
            }

            var tooltipLike = Globalize.translate('TooltipLike');
            var tooltipDislike = Globalize.translate('TooltipDislike');

            if (typeof userData.Likes == "undefined") {
                html += LibraryBrowser.getUserDataButtonHtml('markDislike', itemId, 'btnUserItemRating btnUserItemRatingOff', 'fa-thumbs-down', tooltipDislike);
                html += LibraryBrowser.getUserDataButtonHtml('markLike', itemId, 'btnUserItemRating btnUserItemRatingOff', 'fa-thumbs-up', tooltipLike);
            }
            else if (userData.Likes) {
                html += LibraryBrowser.getUserDataButtonHtml('markDislike', itemId, 'btnUserItemRating btnUserItemRatingOff', 'fa-thumbs-down', tooltipDislike);
                html += LibraryBrowser.getUserDataButtonHtml('markLike', itemId, 'btnUserItemRating', 'fa-thumbs-up', tooltipLike);
            }
            else {
                html += LibraryBrowser.getUserDataButtonHtml('markDislike', itemId, 'btnUserItemRating', 'fa-thumbs-down', tooltipDislike);
                html += LibraryBrowser.getUserDataButtonHtml('markLike', itemId, 'btnUserItemRating btnUserItemRatingOff', 'fa-thumbs-up', tooltipLike);
            }

            var tooltipFavorite = Globalize.translate('TooltipFavorite');
            if (userData.IsFavorite) {

                html += LibraryBrowser.getUserDataButtonHtml('markFavorite', itemId, 'btnUserItemRating', 'fa-heart', tooltipFavorite);
            } else {
                html += LibraryBrowser.getUserDataButtonHtml('markFavorite', itemId, 'btnUserItemRating btnUserItemRatingOff', 'fa-heart', tooltipFavorite);
            }

            return html;
        },

        markPlayed: function (link) {

            var id = link.getAttribute('data-itemid');

            var $link = $(link);

            var markAsPlayed = $link.hasClass('btnUserItemRatingOff');

            if (markAsPlayed) {
                ApiClient.markPlayed(Dashboard.getCurrentUserId(), id);
                $link.removeClass('btnUserItemRatingOff');
            } else {
                ApiClient.markUnplayed(Dashboard.getCurrentUserId(), id);
                $link.addClass('btnUserItemRatingOff');
            }
        },

        markFavorite: function (link) {

            var id = link.getAttribute('data-itemid');

            var $link = $(link);

            var markAsFavorite = $link.hasClass('btnUserItemRatingOff');

            ApiClient.updateFavoriteStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);

            if (markAsFavorite) {
                $link.removeClass('btnUserItemRatingOff');
            } else {
                $link.addClass('btnUserItemRatingOff');
            }
        },

        markLike: function (link) {

            var id = link.getAttribute('data-itemid');

            var $link = $(link);

            if ($link.hasClass('btnUserItemRatingOff')) {

                ApiClient.updateUserItemRating(Dashboard.getCurrentUserId(), id, true);

                $link.removeClass('btnUserItemRatingOff');

            } else {

                ApiClient.clearUserItemRating(Dashboard.getCurrentUserId(), id);

                $link.addClass('btnUserItemRatingOff');
            }

            $link.prev().addClass('btnUserItemRatingOff');
        },

        markDislike: function (link) {

            var id = link.getAttribute('data-itemid');

            var $link = $(link);

            if ($link.hasClass('btnUserItemRatingOff')) {

                ApiClient.updateUserItemRating(Dashboard.getCurrentUserId(), id, false);

                $link.removeClass('btnUserItemRatingOff');

            } else {

                ApiClient.clearUserItemRating(Dashboard.getCurrentUserId(), id);

                $link.addClass('btnUserItemRatingOff');
            }

            $link.next().addClass('btnUserItemRatingOff');
        },

        getDetailImageHtml: function (item, href, preferThumb) {

            var imageTags = item.ImageTags || {};

            if (item.PrimaryImageTag) {
                imageTags.Primary = item.PrimaryImageTag;
            }

            var html = '';

            var url;

            var imageHeight = 360;

            if (preferThumb && imageTags.Thumb) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    height: imageHeight,
                    tag: item.ImageTags.Thumb
                });
            }
            else if (imageTags.Primary) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Primary",
                    height: imageHeight,
                    tag: item.ImageTags.Primary
                });
            }
            else if (item.BackdropImageTags && item.BackdropImageTags.length) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Backdrop",
                    height: imageHeight,
                    tag: item.BackdropImageTags[0]
                });
            }
            else if (imageTags.Thumb) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    height: imageHeight,
                    tag: item.ImageTags.Thumb
                });
            }
            else if (imageTags.Disc) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Disc",
                    height: imageHeight,
                    tag: item.ImageTags.Disc
                });
            }
            else if (item.AlbumId && item.AlbumPrimaryImageTag) {

                url = ApiClient.getScaledImageUrl(item.AlbumId, {
                    type: "Primary",
                    height: imageHeight,
                    tag: item.AlbumPrimaryImageTag
                });

            }
            else if (item.MediaType == "Audio" || item.Type == "MusicAlbum" || item.Type == "MusicGenre") {
                url = "css/images/items/detail/audio.png";
            }
            else if (item.MediaType == "Game" || item.Type == "GameGenre") {
                url = "css/images/items/detail/game.png";
            }
            else if (item.Type == "Person") {
                url = "css/images/items/detail/person.png";
            }
            else if (item.Type == "Genre" || item.Type == "Studio") {
                url = "css/images/items/detail/video.png";
            }
            else if (item.Type == "TvChannel") {
                url = "css/images/items/detail/tv.png";
            }
            else {
                url = "css/images/items/detail/video.png";
            }

            html += '<div style="position:relative;">';

            if (href) {
                html += "<a class='itemDetailGalleryLink' href='" + href + "'>";
            }

            html += "<img class='itemDetailImage' src='" + url + "' />";
            if (href) {
                html += "</a>";
            }

            var progressHtml = item.IsFolder ? '' : LibraryBrowser.getItemProgressBarHtml((item.Type == 'Recording' ? item : item.UserData));

            if (progressHtml) {
                html += '<div class="detailImageProgressContainer">';
                html += progressHtml;
                html += "</div>";
            }

            html += "</div>";

            return html;
        },

        renderDetailImage: function (elem, item, href, preferThumb) {

            var imageTags = item.ImageTags || {};

            if (item.PrimaryImageTag) {
                imageTags.Primary = item.PrimaryImageTag;
            }

            var html = '';

            var url;
            var shape = 'portrait';

            var imageHeight = 360;
            var detectRatio = false;

            if (preferThumb && imageTags.Thumb) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    height: imageHeight,
                    tag: item.ImageTags.Thumb
                });
                shape = 'thumb';
            }
            else if (imageTags.Primary) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Primary",
                    height: imageHeight,
                    tag: item.ImageTags.Primary
                });
                detectRatio = true;
            }
            else if (item.BackdropImageTags && item.BackdropImageTags.length) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Backdrop",
                    height: imageHeight,
                    tag: item.BackdropImageTags[0]
                });
                shape = 'thumb';
            }
            else if (imageTags.Thumb) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Thumb",
                    height: imageHeight,
                    tag: item.ImageTags.Thumb
                });
                shape = 'thumb';
            }
            else if (imageTags.Disc) {

                url = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Disc",
                    height: imageHeight,
                    tag: item.ImageTags.Disc
                });
                shape = 'square';
            }
            else if (item.AlbumId && item.AlbumPrimaryImageTag) {

                url = ApiClient.getScaledImageUrl(item.AlbumId, {
                    type: "Primary",
                    height: imageHeight,
                    tag: item.AlbumPrimaryImageTag
                });
                shape = 'square';
            }
            else if (item.MediaType == "Audio" || item.Type == "MusicAlbum" || item.Type == "MusicGenre") {
                url = "css/images/items/detail/audio.png";
                shape = 'square';
            }
            else if (item.MediaType == "Game" || item.Type == "GameGenre") {
                url = "css/images/items/detail/game.png";
                shape = 'square';
            }
            else if (item.Type == "Person") {
                url = "css/images/items/detail/person.png";
                shape = 'square';
            }
            else if (item.Type == "Genre" || item.Type == "Studio") {
                url = "css/images/items/detail/video.png";
                shape = 'square';
            }
            else if (item.Type == "TvChannel") {
                url = "css/images/items/detail/tv.png";
                shape = 'square';
            }
            else {
                url = "css/images/items/detail/video.png";
                shape = 'square';
            }

            html += '<div style="position:relative;">';

            if (href) {
                html += "<a class='itemDetailGalleryLink' href='" + href + "'>";
            }

            if (detectRatio && item.PrimaryImageAspectRatio) {

                if (item.PrimaryImageAspectRatio >= 1.48) {
                    shape = 'thumb';
                } else if (item.PrimaryImageAspectRatio >= .85 && item.PrimaryImageAspectRatio <= 1.34) {
                    shape = 'square';
                }
            }

            var screenWidth = $(window).width();

            // Take a guess about whether we should lazy load or not
            if (screenWidth > 600) {
                html += "<img class='itemDetailImage' src='" + url + "' />";
            } else {
                html += "<img class='itemDetailImage lazy' data-src='" + url + "' src='css/images/empty.png' />";
            }

            if (href) {
                html += "</a>";
            }

            var progressHtml = item.IsFolder ? '' : LibraryBrowser.getItemProgressBarHtml((item.Type == 'Recording' ? item : item.UserData));

            if (progressHtml) {
                html += '<div class="detailImageProgressContainer">';
                html += progressHtml;
                html += "</div>";
            }

            html += "</div>";

            elem.html(html);

            var page = $(elem).parents('.page');

            var detailContentEffectedByImage = $('.detailContentEffectedByImage', page);

            if (shape == 'thumb') {
                detailContentEffectedByImage.addClass('detailContentEffectedByThumbImage');
                detailContentEffectedByImage.removeClass('detailContentEffectedBySquareImage');
                detailContentEffectedByImage.removeClass('detailContentEffectedByPortraitImage');

                elem.addClass('thumbDetailImageContainer');
                elem.removeClass('portraitDetailImageContainer');
                elem.removeClass('squareDetailImageContainer');
            }
            else if (shape == 'square') {
                detailContentEffectedByImage.removeClass('detailContentEffectedByThumbImage');
                detailContentEffectedByImage.addClass('detailContentEffectedBySquareImage');
                detailContentEffectedByImage.removeClass('detailContentEffectedByPortraitImage');

                elem.removeClass('thumbDetailImageContainer');
                elem.removeClass('portraitDetailImageContainer');
                elem.addClass('squareDetailImageContainer');
            } else {
                detailContentEffectedByImage.removeClass('detailContentEffectedByThumbImage');
                detailContentEffectedByImage.removeClass('detailContentEffectedBySquareImage');
                detailContentEffectedByImage.addClass('detailContentEffectedByPortraitImage');

                elem.removeClass('thumbDetailImageContainer');
                elem.addClass('portraitDetailImageContainer');
                elem.removeClass('squareDetailImageContainer');
            }

            elem.lazyChildren();
        },

        getMiscInfoHtml: function (item) {

            var miscInfo = [];
            var text, date;

            if (item.Type == "Episode" || item.MediaType == 'Photo') {

                if (item.PremiereDate) {

                    try {
                        date = parseISO8601Date(item.PremiereDate, { toLocal: true });

                        text = date.toLocaleDateString();
                        miscInfo.push(text);
                    }
                    catch (e) {
                        console.log("Error parsing date: " + item.PremiereDate);
                    }
                }
            }

            if (item.StartDate) {

                try {
                    date = parseISO8601Date(item.StartDate, { toLocal: true });

                    text = date.toLocaleDateString();
                    miscInfo.push(text);

                    if (item.Type != "Recording") {
                        text = LiveTvHelpers.getDisplayTime(date);
                        miscInfo.push(text);
                    }
                }
                catch (e) {
                    console.log("Error parsing date: " + item.PremiereDate);
                }
            }

            if (item.ProductionYear && item.Type == "Series") {

                if (item.Status == "Continuing") {
                    miscInfo.push(Globalize.translate('ValueSeriesYearToPresent', item.ProductionYear));

                }
                else if (item.ProductionYear) {

                    text = item.ProductionYear;

                    if (item.EndDate) {

                        try {

                            var endYear = parseISO8601Date(item.EndDate, { toLocal: true }).getFullYear();

                            if (endYear != item.ProductionYear) {
                                text += "-" + parseISO8601Date(item.EndDate, { toLocal: true }).getFullYear();
                            }

                        }
                        catch (e) {
                            console.log("Error parsing date: " + item.EndDate);
                        }
                    }

                    miscInfo.push(text);
                }
            }

            if (item.Type != "Series" && item.Type != "Episode" && item.MediaType != 'Photo') {

                if (item.ProductionYear) {

                    miscInfo.push(item.ProductionYear);
                }
                else if (item.PremiereDate) {

                    try {
                        text = parseISO8601Date(item.PremiereDate, { toLocal: true }).getFullYear();
                        miscInfo.push(text);
                    }
                    catch (e) {
                        console.log("Error parsing date: " + item.PremiereDate);
                    }
                }
            }

            var minutes;

            if (item.RunTimeTicks && item.Type != "Series") {

                if (item.Type == "Audio") {

                    miscInfo.push(Dashboard.getDisplayTime(item.RunTimeTicks));

                } else {
                    minutes = item.RunTimeTicks / 600000000;

                    minutes = minutes || 1;

                    miscInfo.push(Math.round(minutes) + "min");
                }
            }

            if (item.OfficialRating && item.Type !== "Season" && item.Type !== "Episode") {
                miscInfo.push(item.OfficialRating);
            }

            if (item.Video3DFormat) {
                miscInfo.push("3D");
            }

            if (item.MediaType == 'Photo' && item.Width && item.Height) {
                miscInfo.push(item.Width + "x" + item.Height);
            }

            return miscInfo.join('&nbsp;&nbsp;&nbsp;&nbsp;');
        },

        renderOverview: function (elem, item) {

            var overview = item.Overview || '';

            elem.html(overview).trigger('create');

            $('a', elem).each(function () {
                $(this).attr("target", "_blank");
            });

            if (overview) {
                elem.removeClass('empty');
            } else {
                elem.addClass('empty');
            }

        },

        renderStudios: function (elem, item, context) {

            if (item.Studios && item.Studios.length && item.Type != "Series") {

                var html = '';

                for (var i = 0, length = item.Studios.length; i < length; i++) {

                    if (i > 0) {
                        html += '&nbsp;&nbsp;/&nbsp;&nbsp;';
                    }

                    html += '<a class="textlink" href="itembynamedetails.html?context=' + context + '&id=' + item.Studios[i].Id + '">' + item.Studios[i].Name + '</a>';
                }

                var translationKey = item.Studios.length > 1 ? "ValueStudios" : "ValueStudio";

                html = Globalize.translate(translationKey, html);

                elem.show().html(html).trigger('create');


            } else {
                elem.hide();
            }
        },

        renderGenres: function (elem, item, context, limit) {

            var html = '';

            var genres = item.Genres || [];

            for (var i = 0, length = genres.length; i < length; i++) {

                if (limit && i >= limit) {
                    break;
                }

                if (i > 0) {
                    html += '<span>&nbsp;&nbsp;/&nbsp;&nbsp;</span>';
                }

                var param = item.Type == "Audio" || item.Type == "MusicArtist" || item.Type == "MusicAlbum" ? "musicgenre" : "genre";

                if (item.MediaType == "Game") {
                    param = "gamegenre";
                }

                html += '<a class="textlink" href="itembynamedetails.html?context=' + context + '&' + param + '=' + ApiClient.encodeName(genres[i]) + '">' + genres[i] + '</a>';
            }

            elem.html(html).trigger('create');
        },

        renderPremiereDate: function (elem, item) {
            if (item.PremiereDate) {
                try {

                    var date = parseISO8601Date(item.PremiereDate, { toLocal: true });

                    var translationKey = new Date().getTime() > date.getTime() ? "ValuePremiered" : "ValuePremieres";

                    elem.show().html(Globalize.translate(translationKey, date.toLocaleDateString()));

                } catch (err) {
                    elem.hide();
                }
            } else {
                elem.hide();
            }
        },

        renderBudget: function (elem, item) {

            if (item.Budget) {

                elem.show().html(Globalize.translate('ValueBudget', '$' + item.Budget));
            } else {
                elem.hide();
            }
        },

        renderRevenue: function (elem, item) {

            if (item.Revenue) {

                elem.show().html(Globalize.translate('ValueRevenue', '$' + item.Revenue));
            } else {
                elem.hide();
            }
        },

        renderAwardSummary: function (elem, item) {
            if (item.AwardSummary) {
                elem.show().html(Globalize.translate('ValueAwards', item.AwardSummary));
            } else {
                elem.hide();
            }
        },

        renderDetailPageBackdrop: function (page, item) {

            var screenWidth = Math.max(screen.height, screen.width);

            var imgUrl;

            if (item.BackdropImageTags && item.BackdropImageTags.length) {

                imgUrl = ApiClient.getScaledImageUrl(item.Id, {
                    type: "Backdrop",
                    index: 0,
                    maxWidth: screenWidth,
                    tag: item.BackdropImageTags[0]
                });

                $('#itemBackdrop', page).removeClass('noBackdrop').lazyImage(imgUrl);
            }
            else if (item.ParentBackdropItemId && item.ParentBackdropImageTags && item.ParentBackdropImageTags.length) {

                imgUrl = ApiClient.getScaledImageUrl(item.ParentBackdropItemId, {
                    type: 'Backdrop',
                    index: 0,
                    tag: item.ParentBackdropImageTags[0],
                    maxWidth: screenWidth
                });

                $('#itemBackdrop', page).removeClass('noBackdrop').lazyImage(imgUrl);

            }
            else {

                $('#itemBackdrop', page).addClass('noBackdrop').css('background-image', 'none');
            }
        }
    };

})(window, document, jQuery, screen, window.store);