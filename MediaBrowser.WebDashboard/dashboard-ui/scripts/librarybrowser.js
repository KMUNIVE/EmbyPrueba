﻿var LibraryBrowser = (function (window, document, $, screen, localStorage) {

    var defaultBackground = "#555;";

    return {

        getDefaultPageSize: function () {

            var saved = localStorage.getItem('pagesize');

            if (saved) {
                return parseInt(saved);
            }

            if (window.location.toString().toLowerCase().indexOf('localhost') != -1) {
                return 100;
            }
            return 20;
        },

        loadSavedQueryValues: function (key, query) {

            //var values = localStorage.getItem(key + '_' + Dashboard.getCurrentUserId());

            //if (values) {

            //    values = JSON.parse(values);

            //    return $.extend(query, values);
            //}

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

            localStorage.setItem(key + '_' + Dashboard.getCurrentUserId(), JSON.stringify(values));
        },

        getDateParamValue: function (date) {

            function formatDigit(i) {
                return i < 10 ? "0" + i : i;
            }

            var d = date;

            return "" + d.getFullYear() + formatDigit(d.getMonth() + 1) + formatDigit(d.getDate()) + formatDigit(d.getHours()) + formatDigit(d.getMinutes()) + formatDigit(d.getSeconds());
        },

        getPosterDetailViewHtml: function (options) {

            var items = options.items;
            var currentIndexValue;

            if (!options.shape) {
                options.shape = options.preferBackdrop ? "backdrop" : "poster";
            }

            var html = '';

            for (var i = 0, length = items.length; i < length; i++) {

                var item = items[i];

                if (options.timeline) {
                    var year = item.ProductionYear || "Unknown Year";

                    if (year != currentIndexValue) {

                        html += '<h2 class="timelineHeader detailSectionHeader">' + year + '</h2>';
                        currentIndexValue = year;
                    }
                }

                var imgUrl = null;
                var isDefault = false;
                var height = null;

                var cssClass = "tileItem";

                if (options.shape) {
                    cssClass += " " + options.shape + "TileItem";
                }

                html += '<a class="' + cssClass + '" href="' + LibraryBrowser.getHref(item, options.context) + '">';

                if (options.preferBackdrop && item.BackdropImageTags && item.BackdropImageTags.length) {

                    imgUrl = LibraryBrowser.getImageUrl(item, 'Backdrop', 0, {
                        height: 198,
                        width: 352
                    });

                }
                else if (options.preferBackdrop && item.ImageTags && item.ImageTags.Thumb) {

                    imgUrl = ApiClient.getImageUrl(item.Id, {
                        type: "Thumb",
                        height: 198,
                        width: 352,
                        tag: item.ImageTags.Thumb
                    });
                }
                else if (item.ImageTags && item.ImageTags.Primary) {

                    height = 300;

                    imgUrl = LibraryBrowser.getImageUrl(item, 'Primary', 0, {
                        maxheight: height
                    });

                }
                else if (item.AlbumId && item.AlbumPrimaryImageTag) {

                    height = 300;

                    imgUrl = ApiClient.getImageUrl(item.AlbumId, {
                        type: "Primary",
                        height: 100,
                        tag: item.AlbumPrimaryImageTag
                    });

                }
                else if (item.BackdropImageTags && item.BackdropImageTags.length) {

                    imgUrl = LibraryBrowser.getImageUrl(item, 'Backdrop', 0, {
                        height: 198,
                        width: 352
                    });
                }
                else if (item.MediaType == "Audio" || item.Type == "MusicAlbum" || item.Type == "MusicArtist") {

                    imgUrl = "css/images/items/list/audio.png";
                    isDefault = true;
                }
                else if (item.MediaType == "Video" || item.Type == "Season" || item.Type == "Series") {

                    imgUrl = "css/images/items/list/video.png";
                    isDefault = true;
                }
                else if (item.Type == "Person") {

                    imgUrl = "css/images/items/list/person.png";
                    isDefault = true;
                }
                else if (item.Type == "MusicArtist") {

                    imgUrl = "css/images/items/list/audiocollection.png";
                    isDefault = true;
                }
                else if (item.MediaType == "Game") {

                    imgUrl = "css/images/items/list/game.png";
                    isDefault = true;
                }
                else if (item.Type == "Studio" || item.Type == "Genre" || item.Type == "MusicGenre" || item.Type == "GameGenre") {

                    if (options.context == "games") {

                        imgUrl = "css/images/items/list/game.png";
                    }
                    else if (options.context == "music") {

                        imgUrl = "css/images/items/list/audio.png";
                    }
                    else if (options.context == "movies") {

                        imgUrl = "css/images/items/list/chapter.png";
                    }
                    else {
                        imgUrl = "css/images/items/list/collection.png";
                    }
                    isDefault = true;
                }
                else {

                    imgUrl = "css/images/items/list/collection.png";
                    isDefault = true;
                }

                cssClass = isDefault ? "tileImage defaultTileImage" : "tileImage";

                html += '<div class="' + cssClass + '" style="background-image: url(\'' + imgUrl + '\');"></div>';

                html += '<div class="tileContent">';

                if (options.showParentName !== false) {
                    if (item.SeriesName || item.Album || item.AlbumArtist) {
                        var seriesName = item.SeriesName || item.Album || item.AlbumArtist;
                        html += '<div class="tileName">' + seriesName + '</div>';
                    }
                }

                var name = LibraryBrowser.getPosterViewDisplayName(item, options.displayAsSpecial);

                html += '<div class="tileName">' + name + '</div>';

                if (item.CommunityRating || item.CriticRating) {
                    html += '<p>' + LibraryBrowser.getRatingHtml(item) + '</p>';
                }

                var childText = null;

                if (item.Type == "BoxSet") {

                    childText = item.ChildCount == 1 ? "1 Movie" : item.ChildCount + " Movies";

                    html += '<p class="itemMiscInfo">' + childText + '</p>';
                }
                else if (item.Type == "GameSystem") {

                    childText = item.ChildCount == 1 ? "1 Game" : item.ChildCount + " Games";

                    html += '<p class="itemMiscInfo">' + childText + '</p>';
                }
                else if (item.Type == "MusicAlbum") {

                    //childText = item.ChildCount == 1 ? "1 Song" : item.ChildCount + " Songs";

                    //html += '<p class="itemMiscInfo">' + childText + '</p>';
                }
                else if (item.Type == "Genre" || item.Type == "Studio" || item.Type == "Person" || item.Type == "MusicArtist" || item.Type == "MusicGenre" || item.Type == "GameGenre") {

                    html += LibraryBrowser.getItemCountsHtml(options, item);
                }
                else if (item.Type == "Game") {

                    html += '<p class="itemMiscInfo">' + (item.GameSystem) + '</p>';
                }
                else if (item.Type == "Episode") {

                    // Skip it. Just clutter
                }
                else {
                    html += '<p class="itemMiscInfo">' + LibraryBrowser.getMiscInfoHtml(item) + '</p>';
                }

                if (item.Type == "MusicAlbum") {

                    html += '<p class="itemMiscInfo">' + LibraryBrowser.getMiscInfoHtml(item) + '</p>';
                }

                html += '<p class="userDataIcons">' + LibraryBrowser.getUserDataIconsHtml(item) + '</p>';

                html += '</div>';

                if (item.LocationType == "Offline" || item.LocationType == "Virtual") {
                    html += LibraryBrowser.getOfflineIndicatorHtml(item);
                } else {
                    html += LibraryBrowser.getNewIndicatorHtml(item);
                }

                html += "</a>";
            }

            return html;
        },

        getItemCountsHtml: function (options, item) {

            var counts = [];

            var childText;

            if (options.context == "movies") {

                if (item.MovieCount) {

                    childText = item.MovieCount == 1 ? "1 Movie" : item.MovieCount + " Movies";

                    counts.push(childText);
                }
                if (item.TrailerCount) {

                    childText = item.TrailerCount == 1 ? "1 Trailer" : item.TrailerCount + " Trailers";

                    counts.push(childText);
                }

            }
            else if (options.context == "tv") {

                if (item.SeriesCount) {

                    childText = item.SeriesCount == 1 ? "1 Show" : item.SeriesCount + " Shows";

                    counts.push(childText);
                }
                if (item.EpisodeCount) {

                    childText = item.EpisodeCount == 1 ? "1 Episode" : item.EpisodeCount + " Episodes";

                    counts.push(childText);
                }

            }
            else if (options.context == "games") {

                if (item.GameCount) {

                    childText = item.GameCount == 1 ? "1 Game" : item.GameCount + " Games";

                    counts.push(childText);
                }
            }
            else if (options.context == "music") {

                if (item.AlbumCount) {

                    childText = item.AlbumCount == 1 ? "1 Album" : item.AlbumCount + " Albums";

                    counts.push(childText);
                }
                if (item.SongCount) {

                    childText = item.SongCount == 1 ? "1 Song" : item.SongCount + " Songs";

                    counts.push(childText);
                }
                if (item.MusicVideoCount) {

                    childText = item.MusicVideoCount == 1 ? "1 Music Video" : item.MusicVideoCount + " Music Videos";

                    counts.push(childText);
                }
            }

            return counts.length ? '<p class="itemMiscInfo">' + counts.join(' • ') + '</p>' : '';
        },

        getSongHeaderCellHtml: function (text, cssClass, enableSorting, sortField, selectedSortField, sortDirection) {

            var html = cssClass ? '<th class="' + cssClass + '">' : '<th>';

            if (text && enableSorting) {
                html += '<a class="lnkColumnSort" data-sortfield="' + sortField + '" href="#" style="text-decoration:underline;">';
            }

            html += text;

            if (text && enableSorting) {

                html += '</a>';

                if (sortField == selectedSortField) {

                    if (sortDirection == "Descending") {
                        html += '<span style="font-weight:bold;margin-left:3px;">&darr;</span>';
                    } else {
                        html += '<span style="font-weight:bold;margin-left:3px;">&uarr;</span>';
                    }
                }
            }

            html += '</th>';

            return html;
        },

        getSongTableHtml: function (items, options) {

            options = options || {};

            var html = '';

            var cssClass = "detailTable";

            html += '<div class="detailTableContainer"><table class="' + cssClass + '">';

            html += '<tr>';

            html += LibraryBrowser.getSongHeaderCellHtml('', '', options.enableColumnSorting);
            html += LibraryBrowser.getSongHeaderCellHtml('', '', options.enableColumnSorting);
            html += LibraryBrowser.getSongHeaderCellHtml('Track', '', options.enableColumnSorting, 'Name', options.sortBy, options.sortOrder);

            if (options.showAlbum) {
                html += LibraryBrowser.getSongHeaderCellHtml('Album', '', options.enableColumnSorting, 'Album,SortName', options.sortBy, options.sortOrder);
            }
            if (options.showArtist) {
                html += LibraryBrowser.getSongHeaderCellHtml('Album Artist', 'tabletColumn', options.enableColumnSorting, 'AlbumArtist,Album,SortName', options.sortBy, options.sortOrder);
                html += LibraryBrowser.getSongHeaderCellHtml('Artist', '', options.enableColumnSorting, 'Artist,Album,SortName', options.sortBy, options.sortOrder);
            }

            html += LibraryBrowser.getSongHeaderCellHtml('Runtime', 'tabletColumn', options.enableColumnSorting, 'Runtime,AlbumArtist,Album,SortName', options.sortBy, options.sortOrder);
            html += LibraryBrowser.getSongHeaderCellHtml('Plays', 'desktopColumn', options.enableColumnSorting, 'PlayCount,AlbumArtist,Album,SortName', options.sortBy, options.sortOrder);
            html += LibraryBrowser.getSongHeaderCellHtml('', 'desktopColumn userDataCell', options.enableColumnSorting);

            html += '</tr>';

            for (var i = 0, length = items.length; i < length; i++) {

                var item = items[i];

                html += '<tr>';

                html += '<td><button class="btnPlay" type="button" data-role="none" onclick="LibraryBrowser.showPlayMenu(this, \'' + item.Id + '\', \'Audio\', \'Audio\');"><img src="css/images/media/playCircle.png" style="height: 20px;"></button></td>';

                var num = item.IndexNumber;

                if (num && item.ParentIndexNumber) {
                    num = item.ParentIndexNumber + "." + num;
                }
                html += '<td>' + (num || "") + '</td>';

                html += '<td><a href="' + LibraryBrowser.getHref(item, "music") + '">' + (item.Name || "") + '</a></td>';

                if (options.showAlbum) {
                    if (item.Album && item.ParentId) {
                        html += '<td><a href="itemdetails.html?id=' + item.ParentId + '">' + item.Album + '</a></td>';
                    } else {
                        html += '<td>' + (item.Album || '') + '</td>';
                    }
                }

                if (options.showArtist) {

                    if (item.AlbumArtist) {

                        html += '<td class="tabletColumn"><a href="itembynamedetails.html?context=music&musicartist=' + ApiClient.encodeName(item.AlbumArtist) + '">' + item.AlbumArtist + '</a></td>';

                    } else {
                        html += '<td class="tabletColumn"></td>';
                    }
                }

                if (options.showArtist) {

                    if (item.Artists && item.Artists.length) {

                        var artistLinksHtml = LibraryBrowser.getArtistLinksHtml(item.Artists);

                        html += '<td>' + artistLinksHtml + '</td>';
                    }
                    else {
                        html += '<td></td>';
                    }
                }

                var time = Dashboard.getDisplayTime(item.RunTimeTicks || 0);

                html += '<td class="tabletColumn">' + time + '</td>';

                html += '<td class="desktopColumn">' + (item.UserData ? item.UserData.PlayCount : 0) + '</td>';

                html += '<td class="desktopColumn userDataCell">' + LibraryBrowser.getUserDataIconsHtml(item) + '</td>';

                html += '</tr>';
            }

            html += '</table></div>';

            return html;
        },

        getArtistLinksHtml: function (artists) {

            var html = [];

            for (var i = 0, length = artists.length; i < length; i++) {

                var artist = artists[i];

                html.push('<a href="itembynamedetails.html?context=music&musicartist=' + ApiClient.encodeName(artist) + '">' + artist + '</a>');

            }

            html = html.join(' / ');

            return html;
        },

        showPlayMenu: function (positionTo, itemId, itemType, mediaType, resumePositionTicks) {

            var isPlaying = MediaPlayer.isPlaying();

            if (!isPlaying && !resumePositionTicks && mediaType != "Audio") {
                MediaPlayer.playById(itemId, itemType);
                return;
            }

            $('.playFlyout').popup("close").remove();

            var html = '<div data-role="popup" class="playFlyout" style="max-width:300px;" data-theme="a" data-history="false">';

            html += '<ul data-role="listview" style="min-width: 150px;">';
            html += '<li data-role="list-divider" data-theme="b">Play Menu</li>';

            if (itemType == "MusicArtist") {
                html += '<li><a href="#" onclick="MediaPlayer.playArtist(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Play</a></li>';
            } else if (itemType != "MusicGenre") {
                html += '<li><a href="#" onclick="MediaPlayer.playById(\'' + itemId + '\', \'' + itemType + '\');LibraryBrowser.closePlayMenu();">Play</a></li>';
            }

            if (itemType == "Audio") {
                html += '<li><a href="#" onclick="MediaPlayer.playInstantMixFromSong(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Instant Mix</a></li>';
            }
            else if (itemType == "MusicAlbum") {
                html += '<li><a href="#" onclick="MediaPlayer.playInstantMixFromAlbum(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Instant Mix</a></li>';
                html += '<li><a href="#" onclick="MediaPlayer.shuffleFolder(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Shuffle</a></li>';
            }
            else if (itemType == "MusicArtist") {
                html += '<li><a href="#" onclick="MediaPlayer.playInstantMixFromArtist(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Instant Mix</a></li>';
                html += '<li><a href="#" onclick="MediaPlayer.shuffleArtist(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Shuffle</a></li>';
            }
            else if (itemType == "MusicGenre") {
                html += '<li><a href="#" onclick="MediaPlayer.playInstantMixFromMusicGenre(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Instant Mix</a></li>';
                html += '<li><a href="#" onclick="MediaPlayer.shuffleMusicGenre(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Shuffle</a></li>';
            }

            if (resumePositionTicks) {
                html += '<li><a href="#" onclick="MediaPlayer.playById(\'' + itemId + '\', \'' + itemType + '\', ' + resumePositionTicks + ');LibraryBrowser.closePlayMenu();">Resume</a></li>';
            }

            if (isPlaying) {
                if (itemType == "MusicArtist") {
                    html += '<li><a href="#" onclick="MediaPlayer.queueArtist(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Queue</a></li>';
                } else if (itemType != "MusicGenre") {
                    html += '<li><a href="#" onclick="MediaPlayer.queue(\'' + itemId + '\');LibraryBrowser.closePlayMenu();">Queue</a></li>';
                }
            }

            html += '</ul>';

            html += '</div>';

            $($.mobile.activePage).append(html);

            $('.playFlyout').popup({ positionTo: positionTo || "window" }).trigger('create').popup("open").on("popupafterclose", function () {

                $(this).off("popupafterclose").remove();

            }).parents(".ui-popup-container").css("margin-left", 100).css("margin-top", 35);
        },

        closePlayMenu: function () {
            $('.playFlyout').popup("close").remove();
        },

        getHref: function (item, itemByNameContext) {

            if (item.url) {
                return item.url;
            }

            itemByNameContext = itemByNameContext || "";

            // Handle search hints
            var id = item.Id || item.ItemId;

            if (item.Type == "Channel") {
                return "livetvchannel.html?id=" + id;
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
                return "itembynamedetails.html?genre=" + ApiClient.encodeName(item.Name) + "&context=" + itemByNameContext;
            }
            if (item.Type == "MusicGenre") {
                return "itembynamedetails.html?musicgenre=" + ApiClient.encodeName(item.Name) + "&context=" + itemByNameContext;
            }
            if (item.Type == "GameGenre") {
                return "itembynamedetails.html?gamegenre=" + ApiClient.encodeName(item.Name) + "&context=" + itemByNameContext;
            }
            if (item.Type == "Studio") {
                return "itembynamedetails.html?studio=" + ApiClient.encodeName(item.Name) + "&context=" + itemByNameContext;
            }
            if (item.Type == "Person") {
                return "itembynamedetails.html?person=" + ApiClient.encodeName(item.Name) + "&context=" + itemByNameContext;
            }

            if (item.Type == "MusicArtist") {
                if (itemByNameContext == "music") {
                    return "itembynamedetails.html?musicartist=" + ApiClient.encodeName(item.Name) + "&context=" + (itemByNameContext || "music");
                }
            }

            return item.IsFolder ? (id ? "itemlist.html?parentId=" + id : "#") : "itemdetails.html?id=" + id;

        },

        getImageUrl: function (item, type, index, options) {

            options = options || {};
            options.type = type;
            options.index = index;

            if (type == 'Backdrop') {
                options.tag = item.BackdropImageTags[index];
            }
            else if (type == 'Screenshot') {
                options.tag = item.ScreenshotImageTags[index];
            }
            else if (type == 'Primary') {
                options.tag = item.PrimaryImageTag || item.ImageTags[type];
            }
            else {
                options.tag = item.ImageTags[type];
            }

            if (item.Type == "Studio") {

                return ApiClient.getStudioImageUrl(item.Name, options);
            }
            if (item.Type == "Person") {

                return ApiClient.getPersonImageUrl(item.Name, options);
            }
            if (item.Type == "Genre") {

                return ApiClient.getGenreImageUrl(item.Name, options);
            }
            if (item.Type == "MusicGenre") {

                return ApiClient.getMusicGenreImageUrl(item.Name, options);
            }
            if (item.Type == "GameGenre") {

                return ApiClient.getGameGenreImageUrl(item.Name, options);
            }
            if (item.Type == "MusicArtist") {

                return ApiClient.getArtistImageUrl(item.Name, options);
            }

            // For search hints
            return ApiClient.getImageUrl(item.Id || item.ItemId, options);

        },

        getPosterViewHtml: function (options) {

            var items = options.items;
            var currentIndexValue;

            options.shape = options.shape || "portrait";

            var html = "";

            for (var i = 0, length = items.length; i < length; i++) {

                var item = items[i];

                var primaryImageAspectRatio = options.useAverageAspectRatio ? LibraryBrowser.getAveragePrimaryImageAspectRatio([item]) : null;

                var futureDateText;

                if (item.PremiereDate) {
                    try {

                        futureDateText = LibraryBrowser.getFutureDateText(parseISO8601Date(item.PremiereDate, { toLocal: true }), true);

                    } catch (err) {

                    }
                }

                if (options.showPremiereDateIndex && futureDateText) {

                    var val = futureDateText || "Unknown Date";

                    if (val != currentIndexValue) {

                        html += '<h2 class="timelineHeader detailSectionHeader" style="text-align:center;">' + val + '</h2>';
                        currentIndexValue = val;
                    }
                }

                var imgUrl = null;
                var background = null;
                var width = null;
                var height = null;

                var forceName = false;

                if (options.preferBackdrop && item.BackdropImageTags && item.BackdropImageTags.length) {

                    imgUrl = ApiClient.getImageUrl(item.Id, {
                        type: "Backdrop",
                        maxwidth: 576,
                        tag: item.BackdropImageTags[0]
                    });

                }
                else if (options.preferThumb && item.ImageTags && item.ImageTags.Thumb) {

                    imgUrl = ApiClient.getImageUrl(item.Id, {
                        type: "Thumb",
                        maxwidth: 576,
                        tag: item.ImageTags.Thumb
                    });

                }
                else if (options.preferThumb && item.SeriesThumbImageTag) {

                    imgUrl = ApiClient.getImageUrl(item.SeriesId, {
                        type: "Thumb",
                        maxwidth: 576,
                        tag: item.SeriesThumbImageTag
                    });

                }
                else if (options.preferThumb && item.ParentThumbItemId) {

                    imgUrl = ApiClient.getThumbImageUrl(item, {
                        type: "Thumb",
                        maxwidth: 576
                    });

                }
                else if (options.preferThumb && item.BackdropImageTags && item.BackdropImageTags.length) {

                    imgUrl = ApiClient.getImageUrl(item.Id, {
                        type: "Backdrop",
                        maxwidth: 576,
                        tag: item.BackdropImageTags[0]
                    });

                    forceName = true;
                }
                else if (item.ImageTags && item.ImageTags.Primary) {

                    height = 400;
                    width = primaryImageAspectRatio ? Math.round(height * primaryImageAspectRatio) : null;

                    imgUrl = ApiClient.getImageUrl(item.Id, {
                        type: "Primary",
                        height: height,
                        width: width,
                        tag: item.ImageTags.Primary
                    });

                }
                else if (item.AlbumId && item.AlbumPrimaryImageTag) {

                    height = 400;
                    width = primaryImageAspectRatio ? Math.round(height * primaryImageAspectRatio) : null;

                    imgUrl = ApiClient.getImageUrl(item.AlbumId, {
                        type: "Primary",
                        height: height,
                        width: width,
                        tag: item.AlbumPrimaryImageTag
                    });

                }
                else if (item.BackdropImageTags && item.BackdropImageTags.length) {

                    imgUrl = ApiClient.getImageUrl(item.Id, {
                        type: "Backdrop",
                        maxwidth: 576,
                        tag: item.BackdropImageTags[0]
                    });

                }
                else if (item.ImageTags && item.ImageTags.Thumb) {

                    imgUrl = ApiClient.getImageUrl(item, {
                        type: "Thumb",
                        maxwidth: 576,
                        tag: item.ImageTags.Thumb
                    });

                }
                else if (item.SeriesThumbImageTag) {

                    imgUrl = ApiClient.getImageUrl(item.SeriesId, {
                        type: "Thumb",
                        maxwidth: 576,
                        tag: item.SeriesThumbImageTag
                    });

                }
                else if (item.ParentThumbItemId) {

                    imgUrl = ApiClient.getThumbImageUrl(item, {
                        type: "Thumb",
                        maxwidth: 576
                    });

                }
                else if (item.MediaType == "Audio" || item.Type == "MusicAlbum" || item.Type == "MusicArtist") {

                    if (item.Name && options.showTitle) {
                        imgUrl = 'css/images/items/list/audio.png';
                        background = defaultBackground;
                    } else {
                        background = '#555';
                    }
                }
                else if (item.MediaType == "Video" || item.Type == "Season" || item.Type == "Series") {

                    if (item.Name && options.showTitle) {
                        imgUrl = 'css/images/items/list/video.png';
                        background = defaultBackground;
                    } else {
                        background = '#555';
                    }
                }
                else {
                    if (item.Name && options.showTitle) {
                        imgUrl = 'css/images/items/list/collection.png';
                        background = LibraryBrowser.getMetroColor(item.Id);
                    } else {
                        background = '#555';
                    }
                }


                var cssClass = "posterItem";

                if (options.transparent !== false) {
                    cssClass += " transparentPosterItem";
                }

                if (options.borderless) {
                    cssClass += " borderlessPosterItem";
                }

                cssClass += ' ' + options.shape + 'PosterItem';

                html += '<a class="' + cssClass + '" href="' + LibraryBrowser.getHref(item, options.context) + '">';

                var style = "";

                if (imgUrl) {
                    style += 'background-image:url(\'' + imgUrl + '\');';
                }

                if (background) {
                    style += "background-color:" + background + ";";
                }

                html += '<div class="posterItemImage" style="' + style + '"></div>';

                var name = LibraryBrowser.getPosterViewDisplayName(item);

                if (!imgUrl && !options.showTitle) {
                    html += "<div class='posterItemDefaultText'>";
                    html += name;
                    html += "</div>";
                }

                var overlayText = forceName || options.overlayText;

                if (overlayText) {
                    html += '<div class="posterItemTextOverlay">';
                }

                var cssclass = options.centerText ? "posterItemText posterItemTextCentered" : "posterItemText";

                if (options.showParentTitle) {

                    html += "<div class='" + cssclass + "'>";
                    html += item.SeriesName || item.Album || item.AlbumArtist || "&nbsp;";
                    html += "</div>";
                }

                if (options.showTitle || forceName) {

                    html += "<div class='" + cssclass + "'>";
                    html += name;
                    html += "</div>";
                }

                if (options.showPremiereDate && item.PremiereDate) {

                    try {

                        //var date = parseISO8601Date(item.PremiereDate, { toLocal: true });

                        html += "<div class='posterItemText'>";
                        html += LibraryBrowser.getPremiereDateText(item);
                        html += "</div>";

                    } catch (err) {

                    }

                }

                if (options.showProgressBar) {

                    html += "<div class='posterItemText posterItemProgress'>";
                    html += LibraryBrowser.getItemProgressBarHtml(item) || "&nbsp;";
                    html += "</div>";
                }

                if (overlayText) {
                    html += "</div>";
                }

                if (item.LocationType == "Offline" || item.LocationType == "Virtual") {
                    if (options.showLocationTypeIndicator !== false) {
                        html += LibraryBrowser.getOfflineIndicatorHtml(item);
                    }
                } else if (options.showNewIndicator !== false) {
                    html += LibraryBrowser.getNewIndicatorHtml(item);
                }

                html += "</a>";

            }

            return html;
        },

        isYesterday: function (date1) {

            var today = new Date();
            today.setDate(today.getDate() - 1);

            return date1.getFullYear() == today.getFullYear() && date1.getDate() == today.getDate();

        },

        isSameDay: function (date1, date2) {

            return date1.getFullYear() == date2.getFullYear() && date1.getDate() == date2.getDate();

        },

        getFutureDateText: function (date, includeDayNamesInFuture) {

            var weekday = [];
            weekday[0] = "Sunday";
            weekday[1] = "Monday";
            weekday[2] = "Tuesday";
            weekday[3] = "Wednesday";
            weekday[4] = "Thursday";
            weekday[5] = "Friday";
            weekday[6] = "Saturday";

            var currentDate = new Date();

            if (LibraryBrowser.isSameDay(date, currentDate)) {
                return "Today";
            }

            if (LibraryBrowser.isYesterday(date)) {
                return "Yesterday";
            }

            return weekday[date.getDay()] + " " + date.toLocaleDateString();
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

        getPosterViewDisplayName: function (item, displayAsSpecial) {

            var name = item.Name;

            if (displayAsSpecial && item.Type == "Episode" && item.ParentIndexNumber == 0) {

                name = "Special - " + name;

            }
            else if (item.Type == "Episode" && item.IndexNumber != null && item.ParentIndexNumber != null) {

                var displayIndexNumber = item.IndexNumber;

                var number = "S" + item.ParentIndexNumber + ", E" + displayIndexNumber;

                if (item.IndexNumberEnd) {

                    displayIndexNumber = item.IndexNumberEnd;
                    number += "-" + displayIndexNumber;
                }

                name = number + " - " + name;

            }
            else {
                if (item.IndexNumber != null && item.Type !== "Season") {
                    name = item.IndexNumber + " - " + name;
                }
                if (item.ParentIndexNumber != null && item.Type != "Episode") {
                    name = item.ParentIndexNumber + "." + name;
                }
            }

            return name;
        },

        getOfflineIndicatorHtml: function (item) {

            if (item.LocationType == "Offline") {
                return '<div class="posterRibbon offlinePosterRibbon">Offline</div>';
            }

            try {

                var date = parseISO8601Date(item.PremiereDate, { toLocal: true });

                if (item.PremiereDate && (new Date().getTime() < date.getTime())) {
                    return '<div class="posterRibbon unairedPosterRibbon">Unaired</div>';
                }
            } catch (err) {

            }

            if (item.IsFolder) {
                return '';
            }
            return '<div class="posterRibbon missingPosterRibbon">Missing</div>';
        },
        getNewIndicatorHtml: function (item) {

            if (item.LocationType == 'Virtual') {
                return '';
            }

            if (item.Type == "Series") {
                if (item.RecursiveUnplayedItemCount && item.PlayedPercentage) {
                    return '<div class="posterRibbon">' + item.RecursiveUnplayedItemCount + ' New</div>';
                }
            }

            if (!item.IsFolder && item.Type !== "Genre" && item.Type !== "Studio" && item.Type !== "Person" && item.Type !== "MusicArtist" && item.Type !== "MusicGenre" && item.Type !== "GameGenre") {

                var date = item.DateCreated;
                var isPlayed = item.UserData && item.UserData.Played;

                if (!isPlayed) {
                    try {

                        var parsedDate = parseISO8601Date(item.PremiereDate, { toLocal: true });

                        if (date && (new Date().getTime() - parsedDate.getTime()) < 604800000) {
                            return "<div class='posterRibbon'>New</div>";
                        }
                    } catch (err) {

                    }
                }
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
            if (Math.abs(0.66666666667 - result) <= .05) {
                return 0.66666666667;
            }

            // If really close to 16:9 (episode image), just return 16:9
            if (Math.abs(1.777777778 - result) <= .05) {
                return 1.777777778;
            }

            // If really close to 1 (square image), just return 1
            if (Math.abs(1 - result) <= .05) {
                return 1;
            }

            // If really close to 4:3 (poster image), just return 2:3
            if (Math.abs(1.33333333333 - result) <= .05) {
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

        renderName: function (item, nameElem, linkToElement) {

            var name = LibraryBrowser.getPosterViewDisplayName(item);

            Dashboard.setPageTitle(name);

            if (linkToElement) {
                nameElem.html('<a class="detailPageParentLink" href="' + LibraryBrowser.getHref(item) + '">' + name + '</a>').trigger('create');
            } else {
                nameElem.html(name);
            }
        },

        renderParentName: function (item, parentNameElem) {

            var html = [];

            if (item.AlbumArtist && item.Type == "Audio") {
                html.push('<a class="detailPageParentLink" href="itembynamedetails.html?context=music&musicartist=' + ApiClient.encodeName(item.AlbumArtist) + '">' + item.AlbumArtist + '</a>');
            }
            else if (item.AlbumArtist && item.Type == "MusicAlbum") {
                html.push('<a class="detailPageParentLink" href="itembynamedetails.html?context=music&musicartist=' + ApiClient.encodeName(item.AlbumArtist) + '">' + item.AlbumArtist + '</a>');
            }
            else if (item.Artists && item.Artists.length && item.Type == "MusicVideo") {
                html.push('<a class="detailPageParentLink" href="itembynamedetails.html?context=music&musicartist=' + ApiClient.encodeName(item.Artists[0]) + '">' + item.Artists[0] + '</a>');
            }
            else if (item.SeriesName && item.Type == "Episode") {

                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.SeriesId + '">' + item.SeriesName + '</a>');
            }

            if (item.SeriesName && item.Type == "Season") {

                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.SeriesId + '">' + item.SeriesName + '</a>');
            }
            else if (item.ParentIndexNumber != null && item.Type == "Episode") {

                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.SeasonId + '">Season ' + item.ParentIndexNumber + '</a>');
            }
            else if (item.Album && item.Type == "Audio" && (item.AlbumId || item.ParentId)) {
                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + (item.AlbumId || item.ParentId) + '">' + item.Album + '</a>');

            }
            else if (item.Album && item.Type == "MusicVideo" && item.AlbumId) {
                html.push('<a class="detailPageParentLink" href="itemdetails.html?id=' + item.AlbumId + '">' + item.Album + '</a>');

            }
            else if (item.AlbumArtist && item.Type == "MusicAlbum") {

            }
            else if (item.Album) {
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
                links.push('<a class="textlink" href="' + item.HomePageUrl + '" target="_blank">Website</a>');
            }

            var providerIds = item.ProviderIds || {};

            if (providerIds.Imdb) {
                if (item.Type == "Person") {
                    links.push('<a class="textlink" href="http://www.imdb.com/name/' + providerIds.Imdb + '" target="_blank">IMDb</a>');
                }
                else {
                    links.push('<a class="textlink" href="http://www.imdb.com/title/' + providerIds.Imdb + '" target="_blank">IMDb</a>');
                }
            }
            if (providerIds.Tmdb) {
                if (item.Type == "Movie" || item.Type == "Trailer" || item.Type == "MusicVideo")
                    links.push('<a class="textlink" href="http://www.themoviedb.org/movie/' + providerIds.Tmdb + '" target="_blank">TheMovieDB</a>');
                else if (item.Type == "BoxSet")
                    links.push('<a class="textlink" href="http://www.themoviedb.org/collection/' + providerIds.Tmdb + '" target="_blank">TheMovieDB</a>');
                else if (item.Type == "Person")
                    links.push('<a class="textlink" href="http://www.themoviedb.org/person/' + providerIds.Tmdb + '" target="_blank">TheMovieDB</a>');
            }
            if (providerIds.Tvdb) {

                if (item.Type == "Series") {
                    links.push('<a class="textlink" href="http://thetvdb.com/index.php?tab=series&id=' + providerIds.Tvdb + '" target="_blank">TheTVDB</a>');
                }
            }
            if (providerIds.Tvcom) {
                if (item.Type == "Episode")
                    links.push('<a class="textlink" href="http://www.tv.com/shows/' + providerIds.Tvcom + '" target="_blank">TV.com</a>');
                else if (item.Type == "Person")
                    links.push('<a class="textlink" href="http://www.tv.com/people/' + providerIds.Tvcom + '" target="_blank">TV.com</a>');
            }
            if (providerIds.Musicbrainz) {

                if (item.Type == "MusicArtist") {
                    links.push('<a class="textlink" href="http://musicbrainz.org/artist/' + providerIds.Musicbrainz + '" target="_blank">MusicBrainz</a>');
                } else {
                    links.push('<a class="textlink" href="http://musicbrainz.org/release/' + providerIds.Musicbrainz + '" target="_blank">MusicBrainz Release</a>');
                }

            }
            if (providerIds.MusicBrainzReleaseGroup) {

                links.push('<a class="textlink" href="http://musicbrainz.org/release-group/' + providerIds.MusicBrainzReleaseGroup + '" target="_blank">MusicBrainz Release Group</a>');

            }
            if (providerIds.Gamesdb) {
                links.push('<a class="textlink" href="http://thegamesdb.net/game/' + providerIds.Gamesdb + '" target="_blank">GamesDB</a>');
            }

            if (providerIds.NesBox) {

                if (item.GameSystem == "Nintendo") {
                    links.push('<a class="textlink" href="http://nesbox.com/game/' + providerIds.NesBox + '" target="_blank">NESbox</a>');
                }
                else if (item.GameSystem == "Super Nintendo") {
                    links.push('<a class="textlink" href="http://snesbox.com/game/' + providerIds.NesBox + '" target="_blank">SNESbox</a>');
                }
            }

            if (providerIds.Zap2It)
                links.push('<a class="textlink" href="http://tvlistings.zap2it.com/tv/dexter/' + providerIds.Zap2It + '?aid=zap2it" target="_blank">Zap2It</a>');

            if (links.length) {

                var html = 'Links:&nbsp;&nbsp;' + links.join('&nbsp;&nbsp;/&nbsp;&nbsp;');

                $(linksElem).html(html).trigger('create');

            } else {
                $(linksElem).hide();
            }
        },

        getViewSummaryHtml: function (query, checkedSortOption) {

            var html = '';

            if (query.SortBy) {

                var id = checkedSortOption[0].id;
                var sortBy = checkedSortOption.siblings('label[for=' + id + ']').text();

                html += 'Sorted by ' + sortBy.trim().toLowerCase() + ', ' + (query.SortOrder || 'ascending').toLowerCase();

                if (!checkedSortOption.hasClass('defaultSort')) {
                    //html += '<button class="btnChangeToDefaultSort" type="button" data-icon="delete" data-inline="true" data-mini="true" data-iconpos="notext">Remove</button>';
                }

            }

            return html;
        },

        getPagingHtml: function (query, totalRecordCount, updatePageSizeSetting) {

            if (query.Limit && updatePageSizeSetting !== false) {
                localStorage.setItem('pagesize', query.Limit);
            }

            var html = '';

            var pageCount = Math.ceil(totalRecordCount / query.Limit);
            var pageNumber = (query.StartIndex / query.Limit) + 1;

            var dropdownHtml = '<select class="selectPage" data-enhance="false" data-role="none">';
            for (var i = 1; i <= pageCount; i++) {

                if (i == pageNumber) {
                    dropdownHtml += '<option value="' + i + '" selected="selected">' + i + '</option>';
                } else {
                    dropdownHtml += '<option value="' + i + '">' + i + '</option>';
                }
            }
            dropdownHtml += '</select>';

            var recordsEnd = Math.min(query.StartIndex + query.Limit, totalRecordCount);

            // 20 is the minimum page size
            var showControls = totalRecordCount > 20;

            html += '<div class="listPaging">';

            html += '<span style="margin-right: 10px;">';

            var startAtDisplay = totalRecordCount ? query.StartIndex + 1 : 0;
            html += startAtDisplay + '-' + recordsEnd + ' of ' + totalRecordCount;

            if (showControls) {
                html += ', page ' + dropdownHtml + ' of ' + pageCount;
            }

            html += '</span>';

            if (showControls) {
                html += '<button data-icon="arrow-l" data-iconpos="notext" data-inline="true" data-mini="true" class="btnPreviousPage" ' + (query.StartIndex ? '' : 'disabled') + '>Previous Page</button>';

                html += '<button data-icon="arrow-r" data-iconpos="notext" data-inline="true" data-mini="true" class="btnNextPage" ' + (query.StartIndex + query.Limit > totalRecordCount ? 'disabled' : '') + '>Next Page</button>';

                var id = "selectPageSize" + new Date().getTime();

                var options = '';

                function getOption(val) {

                    if (query.Limit == val) {

                        return '<option value="' + val + '" selected="selected">' + val + '</option>';

                    } else {
                        return '<option value="' + val + '">' + val + '</option>';
                    }
                }

                options += getOption(20);
                options += getOption(50);
                options += getOption(100);
                options += getOption(200);
                options += getOption(300);
                options += getOption(400);
                options += getOption(500);

                // Add styles to defeat jquery mobile
                html += '<label style="display:inline;font-size:inherit;" class="labelPageSize" for="' + id + '">Limit: </label><select class="selectPageSize" id="' + id + '" data-enhance="false" data-role="none">' + options + '</select>';
            }

            html += '</div>';

            return html;
        },

        getRatingHtml: function (item) {

            var html = "";

            if (item.CommunityRating) {
                var rating = item.CommunityRating / 2;

                for (var i = 1; i <= 5; i++) {
                    if (rating <= i - 1) {
                        html += "<div class='starRating emptyStarRating' title='" + item.CommunityRating + "'></div>";
                    }
                    else if (rating < i) {
                        html += "<div class='starRating halfStarRating' title='" + item.CommunityRating + "'></div>";
                    }
                    else {
                        html += "<div class='starRating' title='" + item.CommunityRating + "'></div>";
                    }
                }
            }

            if (item.CriticRating != null) {

                if (item.CriticRating >= 60) {
                    html += '<div class="fresh rottentomatoesicon" title="fresh"></div>';
                } else {
                    html += '<div class="rotten rottentomatoesicon" title="rotten"></div>';
                }

                html += '<div class="criticRating">' + item.CriticRating + '%</div>';
            }

            return html;
        },

        getItemProgressBarHtml: function (item, includeText) {

            var html = '';

            var tooltip;
            var pct;

            if (item.PlayedPercentage) {

                tooltip = item.PlayedPercentage.toFixed(1).toString().replace(".0", '') + '% ';

                if (item.Type == "Series" || item.Type == "Season" || item.Type == "BoxSet") {
                    tooltip += "watched";
                } else {
                    tooltip += "played";
                }

                pct = item.PlayedPercentage;
            }
            else if (item.UserData && item.UserData.PlaybackPositionTicks && item.RunTimeTicks) {

                tooltip = Dashboard.getDisplayTime(item.UserData.PlaybackPositionTicks) + " / " + Dashboard.getDisplayTime(item.RunTimeTicks);

                pct = (item.UserData.PlaybackPositionTicks / item.RunTimeTicks) * 100;
            }

            if (pct && pct < 100) {

                html += '<progress title="' + tooltip + '" class="itemProgressBar" min="0" max="100" value="' + pct + '">';
                html += '</progress>';

                if (includeText) {
                    html += '<span class="itemProgressText">' + tooltip + '</span>';
                }
            }

            return html;
        },

        getUserDataIconsHtml: function (item) {

            var html = '';

            if (item.Type != "Audio" && item.Type != "Season" && item.Type != "Series") {
                html += LibraryBrowser.getItemProgressBarHtml(item, true);
            }

            var userData = item.UserData || {};

            var itemId = item.Id;
            var type = item.Type;

            if (type == "Person") {
                itemId = item.Name;
            }
            else if (type == "Studio") {
                itemId = item.Name;
            }
            else if (type == "Genre") {
                itemId = item.Name;
            }
            else if (type == "MusicGenre") {
                itemId = item.Name;
            }
            else if (type == "GameGenre") {
                itemId = item.Name;
            }
            else if (type == "MusicArtist") {
                itemId = item.Name;
            }

            if ((item.MediaType || item.IsFolder) && item.Type != "Channel" && item.Type != "MusicArtist") {
                if (userData.Played) {
                    html += '<img data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgPlayed" src="css/images/userdata/checkedon.png" alt="Played" title="Played" onclick="LibraryBrowser.markPlayed(this);return false;" />';
                } else {
                    html += '<img data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgPlayedOff" src="css/images/userdata/checkedoff.png" alt="Played" title="Played" onclick="LibraryBrowser.markPlayed(this);return false;" />';
                }
            }

            if (typeof userData.Likes == "undefined") {
                html += '<img onclick="LibraryBrowser.markDislike(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgDislikeOff" src="css/images/userdata/thumbs_down_off.png" alt="Dislike" title="Dislike" />';
                html += '<img onclick="LibraryBrowser.markLike(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgLikeOff" src="css/images/userdata/thumbs_up_off.png" alt="Like" title="Like" />';
            }
            else if (userData.Likes) {
                html += '<img onclick="LibraryBrowser.markDislike(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgDislikeOff" src="css/images/userdata/thumbs_down_off.png" alt="Dislike" title="Dislike" />';
                html += '<img onclick="LibraryBrowser.markLike(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgLike" src="css/images/userdata/thumbs_up_on.png" alt="Like" title="Like" />';
            }
            else {
                html += '<img onclick="LibraryBrowser.markDislike(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgDislike" src="css/images/userdata/thumbs_down_on.png" alt="Dislike" title="Dislike" />';
                html += '<img onclick="LibraryBrowser.markLike(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgLikeOff" src="css/images/userdata/thumbs_up_off.png" alt="Like" title="Like" />';
            }

            if (userData.IsFavorite) {
                html += '<img onclick="LibraryBrowser.markFavorite(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgFavorite" src="css/images/userdata/heart_on.png" alt="Favorite" title="Favorite" />';
            } else {
                html += '<img onclick="LibraryBrowser.markFavorite(this);return false;" data-type="' + type + '" data-itemid="' + itemId + '" class="imgUserItemRating imgFavoriteOff" src="css/images/userdata/heart_off.png" alt="Favorite" title="Favorite" />';
            }

            return html;
        },

        markPlayed: function (link) {

            var id = link.getAttribute('data-itemid');

            var $link = $(link);

            var markAsPlayed = $link.hasClass('imgPlayedOff');

            if (markAsPlayed) {
                ApiClient.markPlayed(Dashboard.getCurrentUserId(), id);
            } else {
                ApiClient.markUnplayed(Dashboard.getCurrentUserId(), id);
            }

            if (markAsPlayed) {
                link.src = "css/images/userdata/checkedon.png";
                $link.addClass('imgPlayed').removeClass('imgPlayedOff');
            } else {
                link.src = "css/images/userdata/checkedoff.png";
                $link.addClass('imgPlayedOff').removeClass('imgPlayed');
            }
        },

        markFavorite: function (link) {

            var id = link.getAttribute('data-itemid');
            var type = link.getAttribute('data-type');

            var $link = $(link);

            var markAsFavorite = $link.hasClass('imgFavoriteOff');

            if (type == "Person") {
                ApiClient.updateFavoritePersonStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);
            }
            else if (type == "Studio") {
                ApiClient.updateFavoriteStudioStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);
            }
            else if (type == "MusicArtist") {
                ApiClient.updateFavoriteArtistStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);
            }
            else if (type == "Genre") {
                ApiClient.updateFavoriteGenreStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);
            }
            else if (type == "MusicGenre") {
                ApiClient.updateFavoriteMusicGenreStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);
            }
            else if (type == "GameGenre") {
                ApiClient.updateFavoriteGameGenreStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);
            }
            else {
                ApiClient.updateFavoriteStatus(Dashboard.getCurrentUserId(), id, markAsFavorite);
            }

            if (markAsFavorite) {
                link.src = "css/images/userdata/heart_on.png";
                $link.addClass('imgFavorite').removeClass('imgFavoriteOff');
            } else {
                link.src = "css/images/userdata/heart_off.png";
                $link.addClass('imgFavoriteOff').removeClass('imgFavorite');
            }
        },

        markLike: function (link) {

            var id = link.getAttribute('data-itemid');
            var type = link.getAttribute('data-type');

            var $link = $(link);

            if ($link.hasClass('imgLikeOff')) {

                LibraryBrowser.updateUserItemRating(type, id, true);

                link.src = "css/images/userdata/thumbs_up_on.png";
                $link.addClass('imgLike').removeClass('imgLikeOff');

            } else {

                LibraryBrowser.clearUserItemRating(type, id);

                link.src = "css/images/userdata/thumbs_up_off.png";
                $link.addClass('imgLikeOff').removeClass('imgLike');
            }

            $link.prev().removeClass('imgDislike').addClass('imgDislikeOff').each(function () {
                this.src = "css/images/userdata/thumbs_down_off.png";
            });
        },

        markDislike: function (link) {

            var id = link.getAttribute('data-itemid');
            var type = link.getAttribute('data-type');

            var $link = $(link);

            if ($link.hasClass('imgDislikeOff')) {

                LibraryBrowser.updateUserItemRating(type, id, false);

                link.src = "css/images/userdata/thumbs_down_on.png";
                $link.addClass('imgDislike').removeClass('imgDislikeOff');

            } else {

                LibraryBrowser.clearUserItemRating(type, id);

                link.src = "css/images/userdata/thumbs_down_off.png";
                $link.addClass('imgDislikeOff').removeClass('imgDislike');
            }

            $link.next().removeClass('imgLike').addClass('imgLikeOff').each(function () {
                this.src = "css/images/userdata/thumbs_up_off.png";
            });
        },

        updateUserItemRating: function (type, id, likes) {

            if (type == "Person") {
                ApiClient.updatePersonRating(Dashboard.getCurrentUserId(), id, likes);
            }
            else if (type == "Studio") {
                ApiClient.updateStudioRating(Dashboard.getCurrentUserId(), id, likes);
            }
            else if (type == "MusicArtist") {
                ApiClient.updateArtistRating(Dashboard.getCurrentUserId(), id, likes);
            }
            else if (type == "Genre") {
                ApiClient.updateGenreRating(Dashboard.getCurrentUserId(), id, likes);
            }
            else if (type == "MusicGenre") {
                ApiClient.updateMusicGenreRating(Dashboard.getCurrentUserId(), id, likes);
            }
            else if (type == "GameGenre") {
                ApiClient.updateGameGenreRating(Dashboard.getCurrentUserId(), id, likes);
            }
            else {
                ApiClient.updateUserItemRating(Dashboard.getCurrentUserId(), id, likes);
            }
        },

        clearUserItemRating: function (type, id) {

            if (type == "Person") {
                ApiClient.clearPersonRating(Dashboard.getCurrentUserId(), id);
            }
            else if (type == "Studio") {
                ApiClient.clearStudioRating(Dashboard.getCurrentUserId(), id);
            }
            else if (type == "MusicArtist") {
                ApiClient.clearArtistRating(Dashboard.getCurrentUserId(), id);
            }
            else if (type == "Genre") {
                ApiClient.clearGenreRating(Dashboard.getCurrentUserId(), id);
            }
            else if (type == "MusicGenre") {
                ApiClient.clearMusicGenreRating(Dashboard.getCurrentUserId(), id);
            }
            else if (type == "GameGenre") {
                ApiClient.clearGameGenreRating(Dashboard.getCurrentUserId(), id);
            }
            else {
                ApiClient.clearUserItemRating(Dashboard.getCurrentUserId(), id);
            }
        },

        getDetailImageHtml: function (item, href) {

            var imageTags = item.ImageTags || {};

            if (item.PrimaryImageTag) {
                imageTags.Primary = item.PrimaryImageTag;
            }

            var html = '';

            var url;

            var imageHeight = 510;

            if (imageTags.Primary) {

                if (item.Type == "Channel") {
                    url = ApiClient.getUrl("LiveTV/Channels/" + item.Id + "/Images/Primary", {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "Recording") {
                    url = ApiClient.getUrl("LiveTV/Recordings/" + item.Id + "/Images/Primary", {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "Program") {
                    url = ApiClient.getUrl("LiveTV/Programs/" + item.Id + "/Images/Primary", {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "Person") {
                    url = ApiClient.getPersonImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "Genre") {
                    url = ApiClient.getGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "MusicGenre") {
                    url = ApiClient.getMusicGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "GameGenre") {
                    url = ApiClient.getGameGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "Studio") {
                    url = ApiClient.getStudioImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else if (item.Type == "MusicArtist") {
                    url = ApiClient.getArtistImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Primary,
                        type: "Primary"
                    });
                }
                else {
                    url = ApiClient.getImageUrl(item.Id, {
                        type: "Primary",
                        maxheight: imageHeight,
                        tag: item.ImageTags.Primary
                    });
                }
            }
            else if (item.BackdropImageTags && item.BackdropImageTags.length) {

                if (item.Type == "Person") {
                    url = ApiClient.getPersonImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: item.BackdropImageTags[0],
                        type: "Backdrop"
                    });
                }
                else if (item.Type == "Genre") {
                    url = ApiClient.getGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: item.BackdropImageTags[0],
                        type: "Backdrop"
                    });
                }
                else if (item.Type == "MusicGenre") {
                    url = ApiClient.getMusicGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: item.BackdropImageTags[0],
                        type: "Backdrop"
                    });
                }
                else if (item.Type == "GameGenre") {
                    url = ApiClient.getGameGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: item.BackdropImageTags[0],
                        type: "Backdrop"
                    });
                }
                else if (item.Type == "Studio") {
                    url = ApiClient.getStudioImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: item.BackdropImageTags[0],
                        type: "Backdrop"
                    });
                }
                else if (item.Type == "MusicArtist") {
                    url = ApiClient.getArtistImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: item.BackdropImageTags[0],
                        type: "Backdrop"
                    });
                }
                else {
                    url = ApiClient.getImageUrl(item.Id, {
                        type: "Backdrop",
                        maxheight: imageHeight,
                        tag: item.BackdropImageTags[0]
                    });
                }
            }
            else if (imageTags.Thumb) {

                if (item.Type == "Person") {
                    url = ApiClient.getPersonImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Thumb,
                        type: "Thumb"
                    });
                }
                else if (item.Type == "Genre") {
                    url = ApiClient.getGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Thumb,
                        type: "Thumb"
                    });
                }
                else if (item.Type == "MusicGenre") {
                    url = ApiClient.getMusicGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Thumb,
                        type: "Thumb"
                    });
                }
                else if (item.Type == "GameGenre") {
                    url = ApiClient.getGameGenreImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Thumb,
                        type: "Thumb"
                    });
                }
                else if (item.Type == "Studio") {
                    url = ApiClient.getStudioImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Thumb,
                        type: "Thumb"
                    });
                }
                else if (item.Type == "MusicArtist") {
                    url = ApiClient.getArtistImageUrl(item.Name, {
                        maxheight: imageHeight,
                        tag: imageTags.Thumb,
                        type: "Thumb"
                    });
                }
                else {
                    url = ApiClient.getImageUrl(item.Id, {
                        type: "Thumb",
                        maxheight: imageHeight,
                        tag: item.ImageTags.Thumb
                    });
                }
            }
            else if (imageTags.Disc) {

                url = ApiClient.getImageUrl(item.Id, {
                    type: "Disc",
                    maxheight: imageHeight,
                    tag: item.ImageTags.Disc
                });
            }
            else if (item.AlbumId && item.AlbumPrimaryImageTag) {

                url = ApiClient.getImageUrl(item.AlbumId, {
                    type: "Primary",
                    maxheight: imageHeight,
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
            else if (item.Type == "Channel") {
                url = "css/images/items/detail/tv.png";
            }
            else {
                url = "css/images/items/detail/video.png";
            }

            var identifierName = "id";
            var identifierValue = item.Id;

            if (item.Type == "Person" || item.Type == "Genre" || item.Type == "Studio" || item.Type == "MusicArtist" || item.Type == "MusicGenre" || item.Type == "GameGenre") {
                identifierName = item.Type;
                identifierValue = ApiClient.encodeName(item.Name);
            }

            if (!href) {
                href = "itemgallery.html?" + identifierName + "=" + identifierValue;
            }

            var linkToGallery = LibraryBrowser.shouldDisplayGallery(item);

            if (linkToGallery) {
                html += "<a class='itemDetailGalleryLink' href='" + href + "'>";
            }

            html += "<img class='itemDetailImage' src='" + url + "' />";

            if (linkToGallery) {
                html += "</a>";
            }

            return html;
        },

        getMiscInfoHtml: function (item) {

            var miscInfo = [];
            var text, date;

            if (item.Type == "Episode") {

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
                    miscInfo.push(item.ProductionYear + "-Present");

                } else if (item.ProductionYear) {

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

            if (item.Type != "Series" && item.Type != "Episode") {

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

            return miscInfo.join('&nbsp;&nbsp;&nbsp;&nbsp;');
        },

        renderOverview: function (elem, item) {

            var overview = item.OverviewHtml || item.Overview || '';

            elem.html(overview).trigger('create');

            $('a', elem).each(function () {
                $(this).attr("target", "_blank");
            });

        },

        renderStudios: function (elem, item, context) {

            if (item.Studios && item.Studios.length && item.Type != "Series") {

                var prefix = item.Studios.length > 1 ? "Studios" : "Studio";
                var html = prefix + ':&nbsp;&nbsp;';

                for (var i = 0, length = item.Studios.length; i < length; i++) {

                    if (i > 0) {
                        html += '&nbsp;&nbsp;/&nbsp;&nbsp;';
                    }

                    html += '<a class="textlink" href="itembynamedetails.html?context=' + context + '&studio=' + ApiClient.encodeName(item.Studios[i].Name) + '">' + item.Studios[i].Name + '</a>';
                }

                elem.show().html(html).trigger('create');


            } else {
                elem.hide();
            }
        },

        renderGenres: function (elem, item, context) {

            var html = '';

            var genres = item.Genres || [];

            for (var i = 0, length = genres.length; i < length; i++) {

                if (i > 0) {
                    html += '&nbsp;&nbsp;/&nbsp;&nbsp;';
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

                    var text = new Date().getTime() > date.getTime() ? "Premiered" : "Premieres";

                    elem.show().html(text + '&nbsp;&nbsp;' + date.toLocaleDateString());
                } catch (err) {
                    elem.hide();
                }
            } else {
                elem.hide();
            }
        },

        renderBudget: function (elem, item) {
            if (item.Budget) {
                elem.show().html('Budget:&nbsp;&nbsp;$<span class="autoNumeric" data-a-pad="false">' + item.Budget + '</span>');
            } else {
                elem.hide();
            }
        },

        renderRevenue: function (elem, item) {
            if (item.Revenue) {
                elem.show().html('Revenue:&nbsp;&nbsp;$<span class="autoNumeric" data-a-pad="false">' + item.Revenue + '</span>');
            } else {
                elem.hide();
            }
        },

        renderDetailPageBackdrop: function (page, item) {

            var screenWidth = Math.max(screen.height, screen.width);

            var imgUrl;

            if (item.BackdropImageTags && item.BackdropImageTags.length) {

                imgUrl = LibraryBrowser.getImageUrl(item, 'Backdrop', 0, {
                    maxwidth: screenWidth
                });

                $('#itemBackdrop', page).removeClass('noBackdrop').css('background-image', 'url("' + imgUrl + '")');

            }
            else if (item.ParentBackdropItemId && item.ParentBackdropImageTags && item.ParentBackdropImageTags.length) {

                imgUrl = ApiClient.getImageUrl(item.ParentBackdropItemId, {
                    type: 'Backdrop',
                    index: 0,
                    tag: item.ParentBackdropImageTags[0],
                    maxwidth: screenWidth
                });

                $('#itemBackdrop', page).removeClass('noBackdrop').css('background-image', 'url("' + imgUrl + '")');

            }
            else {

                $('#itemBackdrop', page).addClass('noBackdrop').css('background-image', 'none');
            }
        },

        shouldDisplayGallery: function (item) {

            var imageTags = item.ImageTags || {};

            if (imageTags.Primary) {

                return true;
            }

            if (imageTags.Banner) {

                return true;
            }

            if (imageTags.Logo) {

                return true;
            }
            if (imageTags.Thumb) {

                return true;
            }
            if (imageTags.Art) {

                return true;

            }
            if (imageTags.Menu) {

                return true;

            }
            if (imageTags.Disc) {

                return true;
            }
            if (imageTags.Box) {

                return true;
            }
            if (imageTags.BoxRear) {

                return true;
            }

            if (item.BackdropImageTags && item.BackdropImageTags.length) {
                return true;

            }

            if (item.ScreenshotImageTags && item.ScreenshotImageTags.length) {
                return true;
            }

            return false;
        },

        getGalleryHtml: function (item) {

            var html = '';
            var i, length;

            var imageTags = item.ImageTags || {};

            if (imageTags.Primary) {

                html += LibraryBrowser.createGalleryImage(item, "Primary", imageTags.Primary);
            }

            if (imageTags.Banner) {

                html += LibraryBrowser.createGalleryImage(item, "Banner", imageTags.Banner);
            }

            if (imageTags.Logo) {

                html += LibraryBrowser.createGalleryImage(item, "Logo", imageTags.Logo);
            }
            if (imageTags.Thumb) {

                html += LibraryBrowser.createGalleryImage(item, "Thumb", imageTags.Thumb);
            }
            if (imageTags.Art) {

                html += LibraryBrowser.createGalleryImage(item, "Art", imageTags.Art);

            }
            if (imageTags.Menu) {

                html += LibraryBrowser.createGalleryImage(item, "Menu", imageTags.Menu);

            }
            if (imageTags.Box) {

                html += LibraryBrowser.createGalleryImage(item, "Box", imageTags.Box);
            }
            if (imageTags.BoxRear) {

                html += LibraryBrowser.createGalleryImage(item, "BoxRear", imageTags.BoxRear);
            }

            if (item.BackdropImageTags) {

                for (i = 0, length = item.BackdropImageTags.length; i < length; i++) {
                    html += LibraryBrowser.createGalleryImage(item, "Backdrop", item.BackdropImageTags[i], i);
                }

            }

            if (item.ScreenshotImageTags) {

                for (i = 0, length = item.ScreenshotImageTags.length; i < length; i++) {
                    html += LibraryBrowser.createGalleryImage(item, "Screenshot", item.ScreenshotImageTags[i], i);
                }
            }
            if (imageTags.Disc) {

                html += LibraryBrowser.createGalleryImage(item, "Disc", imageTags.Disc);
            }

            return html;
        },

        createGalleryImage: function (item, type, tag, index) {

            var screenWidth = Math.max(screen.height, screen.width);
            screenWidth = Math.min(screenWidth, 1280);

            var html = '';

            if (typeof (index) == "undefined") index = 0;

            html += '<div class="galleryImageContainer">';
            html += '<a href="#pop_' + index + '_' + tag + '" data-transition="fade" data-rel="popup" data-position-to="window">';
            html += '<img class="galleryImage" src="' + LibraryBrowser.getImageUrl(item, type, index, {
                maxwidth: screenWidth,
                tag: tag
            }) + '" />';
            html += '</div>';

            html += '<div class="galleryPopup" id="pop_' + index + '_' + tag + '" data-role="popup">';
            html += '<a href="#" data-rel="back" data-role="button" data-icon="delete" data-iconpos="notext" class="ui-btn-right">Close</a>';
            html += '<img class="" src="' + LibraryBrowser.getImageUrl(item, type, index, {

                maxwidth: screenWidth,
                tag: tag

            }) + '" />';
            html += '</div>';

            return html;
        }

    };

})(window, document, jQuery, screen, localStorage);


(function (window, document, $) {

    var itemCountsPromise;
    var liveTvServicesPromise;

    function renderHeader(page, user) {

        var html = '<div class="viewMenuBar">';

        html += '<a class="viewMenuLink homeMenuLink" href="index.html" title="Home"><img src="css/images/mblogoicon.png" alt="Home" /></a>';

        html += '<a class="viewMenuLink viewMenuImageLink remoteControlMenuLink" href="#" onclick="RemoteControl.showMenu();" title="Remote Control"><img src="css/images/remote.png" alt="Remote Control" /></a>';

        if (user.Configuration.IsAdministrator) {
            html += '<a class="viewMenuLink viewMenuImageLink editorMenuLink" href="edititemmetadata.html" title="Metadata Manager"><img src="css/images/editor.png" alt=""Metadata Manager" /></a>';
        }

        html += '<div class="viewMenuSecondary">';

        html += Search.getSearchHtml();

        html += '<a class="viewMenuLink" class="btnCurrentUser" href="#" onclick="Dashboard.showUserFlyout(this);">';

        if (user.PrimaryImageTag) {

            var url = ApiClient.getUserImageUrl(user.Id, {
                height: 40,
                tag: user.PrimaryImageTag,
                type: "Primary"
            });

            html += '<img src="' + url + '" />';
        } else {
            html += '<img src="css/images/currentuserdefaultwhite.png" />';
        }

        html += '</a>';

        if (user.Configuration.IsAdministrator) {
            html += '<a class="viewMenuLink" href="dashboard.html" title="Dashboard"><img src="css/images/toolswhite.png" alt="Dashboard" /></a>';
        }

        html += '</div>';

        html += '</div>';

        $(page).prepend(html);

        Search.onSearchRendered($('.viewMenuBar', page));
    }

    function insertViews(page, user, counts, liveTvServices) {

        var html = '';

        var selectedCssClass = ' selectedViewLink';
        var selectedHtml = "<span class='selectedViewIndicator'>&#9654;</span>";

        var view = page.getAttribute('data-view') || getParameterByName('context');

        var viewCount = 0;

        if (counts.MovieCount || counts.TrailerCount) {

            html += '<a class="viewMenuLink viewMenuTextLink' + (view == 'movies' ? selectedCssClass : '') + '" href="moviesrecommended.html">' + (view == 'movies' ? selectedHtml : '') + '<span class="viewName">Movies</span></a>';
            viewCount++;
        }

        if (counts.EpisodeCount || counts.SeriesCount) {
            html += '<a class="viewMenuLink viewMenuTextLink' + (view == 'tv' ? selectedCssClass : '') + '" href="tvrecommended.html">' + (view == 'tv' ? selectedHtml : '') + '<span class="viewName">TV</span></a>';
            viewCount++;
        }

        if (liveTvServices.length) {
            html += '<a class="viewMenuLink viewMenuTextLink' + (view == 'livetv' ? selectedCssClass : '') + '" href="livetvchannels.html">' + (view == 'livetv' ? selectedHtml : '') + '<span class="viewName">Live TV</span></a>';
            viewCount++;
        }

        if (counts.SongCount || counts.MusicVideoCount) {
            html += '<a class="viewMenuLink viewMenuTextLink' + (view == 'music' ? selectedCssClass : '') + '" href="musicrecommended.html">' + (view == 'music' ? selectedHtml : '') + '<span class="viewName">Music</span></a>';
            viewCount++;
        }

        if (counts.GameCount) {
            html += '<a class="viewMenuLink viewMenuTextLink' + (view == 'games' ? selectedCssClass : '') + '" href="gamesrecommended.html">' + (view == 'games' ? selectedHtml : '') + '<span class="viewName">Games</span></a>';
            viewCount++;
        }

        $(page).addClass('pageViewCount' + viewCount);

        $('.homeMenuLink', page).after(html);
    }

    $(document).on('pagebeforeshow', ".libraryPage", function () {

        var page = this;

        if (!$('.viewMenuBar', page).length) {

            Dashboard.getCurrentUser().done(function (user) {

                renderHeader(page, user);

                itemCountsPromise = itemCountsPromise || ApiClient.getItemCounts(Dashboard.getCurrentUserId());
                liveTvServicesPromise = liveTvServicesPromise || ApiClient.getLiveTvServices();

                $.when(itemCountsPromise, liveTvServicesPromise).done(function (response1, response2) {

                    var counts = response1[0];
                    var liveTvServices = response2[0];

                    insertViews(page, user, counts, liveTvServices);


                });
            });
        }
    });

})(window, document, jQuery);

